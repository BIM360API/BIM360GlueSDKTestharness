// Copyright 2012 Autodesk, Inc.  All rights reserved.
// Use of this software is subject to the terms of the Autodesk license agreement 
// provided at the time of installation or download, or which otherwise accompanies 
// this software in either electronic or hard copy form.   

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using System.Web.Script.Serialization;
using Utility.ModifyRegistry;
using Microsoft.Win32;
using System.Web;

namespace BIM360SDKTestClient
{
	public partial class ClientForm : Form
	{
    // App Configuration Options
    static BIM360DisplayComponent gViewer = null;
    static string regSubKey = "SOFTWARE\\Autodesk\\TestHarness\\Settings";
    Timer tTimer = new Timer();

    // Internal Classes
    public class glueResponse
    {
      public HttpStatusCode statusCode { get; set; }
      public string verboseResponse { get; set; }
      public string responseBody { get; set; }
    }

    //===============================================================
    // Save and restore registry settings
    //===============================================================
    #region Registry Routines
    void ShowHelperToolTip(object sender, EventArgs e)
    {
      toolTip1.Show("To get started, fill in the information about your\nBIM 360 Glue API Key in this section and press the \"Validate API Key\"\nto check your key information.", txt_base_url, new Point(50, 80));
    }

    void StoreSettings()
    {
      // Setup Registry Save
      ModifyRegistry myRegistry = new ModifyRegistry();
      myRegistry.BaseRegistryKey = Registry.CurrentUser;
      myRegistry.SubKey = regSubKey;

      myRegistry.Write("BASE_URL", txt_base_url.Text);
      myRegistry.Write("API_KEY", txt_api_key.Text);
      myRegistry.Write("API_SECRET", txt_api_secret.Text);
      myRegistry.Write("COMPANY_ID", txt_company_id.Text);
      myRegistry.Write("LOGIN_NAME", txt_login_name.Text);
      myRegistry.Write("AUTH_TOKEN", txt_auth_token.Text);
      myRegistry.Write("USER_ID", txt_my_user_id.Text);
      myRegistry.Write("SERVICE_PROVIDER_KEY", chk_service_provider.Checked);
      myRegistry.Write("BIM360_VIEWER_BASE_URL", txt_viewer_base_url.Text);

      // Password - Yes, this should really be encrypted, but we are going to just do something simple
      // to "scramble" the text
      myRegistry.Write("USER_PASSWORD", Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes(txt_password.Text)));
    }

    void LoadSettings()
    {
      ModifyRegistry myRegistry = new ModifyRegistry();
      myRegistry.BaseRegistryKey = Registry.CurrentUser;
      myRegistry.SubKey = regSubKey;

      txt_base_url.Text = myRegistry.Read("BASE_URL", "http://bim360.autodesk.com/api/");
      txt_api_key.Text = myRegistry.Read("API_KEY", "");
      txt_api_secret.Text = myRegistry.Read("API_SECRET", "");
      txt_company_id.Text = myRegistry.Read("COMPANY_ID", "");
      txt_login_name.Text = myRegistry.Read("LOGIN_NAME", "");
      txt_viewer_base_url.Text = myRegistry.Read("BIM360_VIEWER_BASE_URL", "http://bim360.autodesk.com/BIM360Web32bit/Glue.xbap");

      string loadPW = myRegistry.Read("USER_PASSWORD");
      if (loadPW != null)
      {
        byte[] decbuff = Convert.FromBase64String(loadPW);
        string decPW = System.Text.Encoding.UTF8.GetString(decbuff);
        txt_password.Text = decPW;
      }

      txt_auth_token.Text = myRegistry.Read("AUTH_TOKEN", "");
      txt_my_user_id.Text = myRegistry.Read("USER_ID", "");

      if (Convert.ToBoolean(myRegistry.Read("SERVICE_PROVIDER_KEY")) == true)
      {
        chk_service_provider.Checked = true;
      }

      // Now, setup the state of the app
      VerbComboBox.SelectedIndex = 1;
      combo_response_format.SelectedIndex = 0;
      chk_pretty.Checked = true;

      if (txt_auth_token.Text != "")
      {
        buttonLogin.Enabled = false;
        buttonLogout.Enabled = true;
      }
      else
      {
        buttonLogin.Enabled = true;
        buttonLogout.Enabled = false;
      }
    }

    #endregion Registry Routines

    //===============================================================
    // Routines to generate the signature components
    //===============================================================
    #region Signature Routines
    private string GenerateAPISignature(string aTimestamp)
    {
      // To build a signature, create an MD5 has of the following concatenated information (no delimiters):
      // API Key
      // API Secret
      // Unix Epoch Timestamp
      //
      string baseString = txt_api_key.Text + txt_api_secret.Text + aTimestamp;
      string newSig = ComputeMD5Hash(baseString);
      return newSig;
    }

    public static int GetUNIXEpochTimestamp()
    {
      TimeSpan tSpan = (DateTime.UtcNow - new DateTime(1970, 1, 1));
      int timestamp = (int)tSpan.TotalSeconds;
      return timestamp;
    }

    public string ComputeMD5Hash(string aString)
    {
      // step 1, calculate MD5 hash from aString
      MD5 md5 = System.Security.Cryptography.MD5.Create();
      byte[] inputBytes = System.Text.Encoding.UTF8.GetBytes(aString);
      byte[] hash = md5.ComputeHash(inputBytes);

      // step 2, convert byte array to hex string
      StringBuilder sb = new StringBuilder();
      for (int i = 0; i < hash.Length; i++)
      {
        sb.Append(hash[i].ToString("x2"));
      }
      return sb.ToString();
    }

    #endregion Signature Routines

    //===============================================================
    // Routines to make the HTTP request
    //===============================================================
    #region HTTP Request Routines
    public string handleError(WebException ex)
    {
      string reponseAsString = "";
      HttpStatusCode statusCode = HttpStatusCode.Ambiguous;
      if (((HttpWebResponse)ex.Response) != null)
      {
        statusCode = ((HttpWebResponse)ex.Response).StatusCode;
      }

      try
      {
        ex.Response.GetResponseStream();
      }
      catch (Exception localEx)
      {
        string localMsg = localEx.Message;
      }
      reponseAsString += "ERROR: " + ex.Message;

      if (ex.Response != null)
      {
        if (ex.Response.ContentLength > 0)
        {
          using (Stream stream = ex.Response.GetResponseStream())
          {
            using (StreamReader reader = new StreamReader(stream))
            {
              reponseAsString += "\r\n";
              reponseAsString += "<::--------- Server Response Below ---------::>";
              reponseAsString += "\r\n";
              reponseAsString += reader.ReadToEnd().Trim();
            }
          }
        }
      }

      return reponseAsString;
    }

    glueResponse MakeAPICall(string urlEndpoint, string urlArgs, string reqMethod, string postBody = "", int byteStartRange = 0, int byteEndRange = 0)
		{
      // Busy...
      Cursor.Current = Cursors.WaitCursor;

      ResponseTextBox.Text = "Making Service Call: " + urlEndpoint + "\r\n";

      glueResponse rObj = new glueResponse();
      string reponseAsString = "";
			var url = txt_base_url.Text;
      if (txt_base_url.Text.Substring(txt_base_url.Text.Length - 1) != "/")
      {
        url += "/";
      }
      url += urlEndpoint + "." + combo_response_format.Text;

      if (urlArgs != "")
      {
        url += "?" + urlArgs;
      }
      try
			{
        HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
        request.Method = reqMethod;

        // Set the byte range
        if ((byteStartRange > 0) || (byteEndRange > 0))
        {
          request.AddRange(byteStartRange, byteEndRange);
        }

        // Setup the user agent
        request.UserAgent = "User-Agent: APP_NAME/APP_VERSION (platform=APP_PLATFORM, page=APP_PAGE, info=APP_INFO)";
        if ((reqMethod != "GET") && (reqMethod != "HEAD"))
        {
          SetBody(request, postBody);
        }
        HttpWebResponse response = null;
        // Bump up timeout for debugging purposes (5 mins)
        request.Timeout = 30000;

        // Start execution time tracking
        DateTime startTime = DateTime.Now;

        // For Async requests
        // void FinishWebRequest(IAsyncResult result) { HttpWebResponse response = webRequest.EndGetResponse(result); }
        // response = (HttpWebResponse)request.BeginGetResponse(new AsyncCallback(FinishWebRequest), null);

        response = (HttpWebResponse)request.GetResponse();
        
        // Mark execution time
        float reqTime = (float)(DateTime.Now - startTime).TotalSeconds;
        txtReqTime.Text = reqTime.ToString();
        if (reqTime < .5)
        {
          txtReqTime.BackColor = System.Drawing.Color.Lime;
        }
        else if ((reqTime >= .5) && (reqTime < 1))
        {
          txtReqTime.BackColor = System.Drawing.Color.GreenYellow;
        }
        else if ((reqTime >= 1) && (reqTime < 2))
        {
          txtReqTime.BackColor = System.Drawing.Color.Pink;
        }
        else if ((reqTime >= 2) && (reqTime < 3))
        {
          txtReqTime.BackColor = System.Drawing.Color.Tomato;
        }
        else
        {
          txtReqTime.BackColor = System.Drawing.Color.Red;
        }

        string tHeader = GetHeaderFromResponse(response);
        string tBody = GetBodyFromResponse(response);

        reponseAsString = tHeader + tBody;
        rObj.responseBody = tBody;
        rObj.statusCode = response.StatusCode;

        response.Close();
      }
			catch (WebException ex)
			{
        if (((HttpWebResponse)ex.Response) == null)
        {
          rObj.statusCode = HttpStatusCode.Ambiguous;
        }
        else
        {
          rObj.statusCode = ((HttpWebResponse)ex.Response).StatusCode;
        }
        reponseAsString += handleError(ex);
      }

      rObj.verboseResponse = reponseAsString;

      // Not busy...
      Cursor.Current = Cursors.Default;
      return rObj;
		}

		void SetBody(HttpWebRequest request, string requestBody)
		{
			if (requestBody.Length > 0)
			{
        request.ContentType = "application/x-www-form-urlencoded";
				using (Stream requestStream = request.GetRequestStream())
				using (StreamWriter writer = new StreamWriter(requestStream))
				{
					writer.Write(requestBody);
				}
			}
		}

    string GetHeaderFromResponse(HttpWebResponse response)
    {
      string result = "Status code: " + (int)response.StatusCode + " " + response.StatusCode + "\r\n";
      foreach (string key in response.Headers.Keys)
      {
        result += string.Format("{0}: {1} \r\n", key, response.Headers[key]);
      }

      result += "\r\n";
      return result;
    }

		string GetBodyFromResponse(HttpWebResponse response)
		{
			string result = "";
      string tBody = new StreamReader(response.GetResponseStream()).ReadToEnd();
      result += tBody;
			return result;
		}

    #endregion HTTP Request Routines

    //===============================================================
    // UI Events
    //===============================================================
    #region General UI Routines
    public ClientForm()
		{
			InitializeComponent();
		}

    private void ClientForm_Shown(Object sender, EventArgs e)
    {
      if ((txt_api_key.Text == "") || (txt_api_secret.Text == ""))
      {
        ShowHelperToolTip(sender, e);
      }
    }

	private void ClientForm_Load(object sender, EventArgs e)
		{
      LoadSettings();

      // Event Handlers...
      Closing += new System.ComponentModel.CancelEventHandler(this.ClientForm_Closing);
      Shown += new EventHandler(this.ClientForm_Shown);
      lvUsers.ItemSelectionChanged += new System.Windows.Forms.ListViewItemSelectionChangedEventHandler(UserListView_ItemClick);
      lvProjects.ItemSelectionChanged += new System.Windows.Forms.ListViewItemSelectionChangedEventHandler(ProjectListView_ItemClick);
      lvActionProjects.ItemSelectionChanged += new System.Windows.Forms.ListViewItemSelectionChangedEventHandler(ActionProjectListView_ItemClick);
      lvModelProjects.ItemSelectionChanged += new System.Windows.Forms.ListViewItemSelectionChangedEventHandler(ModelProjectListView_ItemClick);
      lvProjectRoster.ItemSelectionChanged += new System.Windows.Forms.ListViewItemSelectionChangedEventHandler(ProjectRosterListView_ItemClick);
      lvModelModels.ItemSelectionChanged += new System.Windows.Forms.ListViewItemSelectionChangedEventHandler(ModelModelsListView_ItemClick);
      lvViews.ItemSelectionChanged += new System.Windows.Forms.ListViewItemSelectionChangedEventHandler(ModelViews_ItemClick);
      lvActionList.ItemSelectionChanged += new System.Windows.Forms.ListViewItemSelectionChangedEventHandler(ActionSearch_ItemClick);

      tvProject.AfterSelect += new TreeViewEventHandler(TreeView_AfterSelect);

      rnListView.ItemSelectionChanged += RNListView_ItemClick;

      //Adding columns to the ListView control
      lvUsers.Columns.Add("login_name", 75);
      lvUsers.Columns.Add("created_date", 125);
      lvUsers.Columns.Add("user_id", 100);

      //Adding columns to the ListView control
      lvProjects.Columns.Add("project_name", 150);
      lvProjects.Columns.Add("created_date", 125);
      lvProjects.Columns.Add("project_id", 200);

      //Adding columns to the ListView control
      lvProjectRoster.Columns.Add("name", 75);
      lvProjectRoster.Columns.Add("user_id", 75);

      //Adding columns to the ListView control
      lvActionProjects.Columns.Add("project_name", 150);
      lvActionProjects.Columns.Add("created_date", 125);
      lvActionProjects.Columns.Add("project_id", 200);

      //Adding columns to the ListView control
      lvActionList.Columns.Add("action_id", 125);
      lvActionList.Columns.Add("type", 80);
      lvActionList.Columns.Add("type_object_id", 80);
      lvActionList.Columns.Add("subject", 100);
      lvActionList.Columns.Add("model_name", 100);
      lvActionList.Columns.Add("model_id", 100);
      lvActionList.Columns.Add("created_by", 100);
      lvActionList.Columns.Add("created_date", 100);

      //Adding columns to the ListView control
      lvModels.Columns.Add("name", 80);
      lvModels.Columns.Add("model_id", 140);

      //Adding columns to the ListView control
      lvModelProjects.Columns.Add("project_name", 150);
      lvModelProjects.Columns.Add("created_date", 125);
      lvModelProjects.Columns.Add("project_id", 200);

      //Adding columns to the ListView control
      lvModelModels.Columns.Add("name", 80);
      lvModelModels.Columns.Add("ver", 40);
      lvModelModels.Columns.Add("model_id", 80);
      lvModelModels.Columns.Add("version_id", 80);

      //Adding columns to the ListView control
      lvModelInfo.Columns.Add("Attribute", 80);
      lvModelInfo.Columns.Add("Value", 110);

      //Adding columns to the ListView control
      lvSubModels.Columns.Add("Sub-Model", 110);
      lvSubModels.Columns.Add("Version", 80);

      //Adding columns to the ListView control
      lvClashReps.Columns.Add("Clash Rep", 70);
      lvClashReps.Columns.Add("Created By", 110);

      //Adding columns to the ListView control
      lvProjectClashReports.Columns.Add("Clash Rep", 70);
      lvProjectClashReports.Columns.Add("Created By", 110);

      //Adding columns to the ListView control
      lvViews.Columns.Add("type", 60);
      lvViews.Columns.Add("object_name", 80);
      lvViews.Columns.Add("created_date", 125);
      lvViews.Columns.Add("created_by", 120);
      lvViews.Columns.Add("action_id", 120);     

      //Adding columns to the ListView control
      lvMarkups.Columns.Add("Name", 70);
      lvMarkups.Columns.Add("Markup ID", 110);

      //Adding columns to the ListView control
      lvMergedModelComponents.Columns.Add("Model ID", 130);

      //RN Children
      rnListView.Columns.Add("Resource Names", rnListView.Width);

      //tTimer.Interval = 1000;
      //tTimer.Enabled = true;
      //tTimer.Tick += new System.EventHandler(ShowHelperToolTip);
      //tTimer.Start();
    }

    private void ClientForm_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
      // if (MessageBox.Show("Are you sure you want to exit?", "Confirm Exit", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.No)
      // {
      //   e.Cancel = true;
      // }

      StoreSettings();
    }

    private void buttonClear_Click(object sender, EventArgs e)
    {
      ResponseTextBox.Text = "";
    }

    #endregion General UI Routines

    //===============================================================
    // SYSTEM SERVICE GROUP: Client Implementations
    //===============================================================
    #region System Service Group

    private void butValidateKey_Click(object sender, EventArgs e)
    {
      butValidateAPIKey_Click(sender, e);
    }

    private void buttonStatus_Click_1(object sender, EventArgs e)
    {
      string urlArgs = "";
      if (chk_pretty.Checked)
      {
        urlArgs += "pretty=1";
      }
      if (chk_no_http_status.Checked)
      {
        if (urlArgs == "") { urlArgs += "&"; }
        urlArgs += "no_http_status=1";
      }

      glueResponse tResponse = MakeAPICall("system/v1/status", urlArgs, "GET");
      if (tResponse.statusCode == HttpStatusCode.OK)
      {
        toolTip1.Show("The server status looks good!", labHidden, new Point(-100, -25));
      }
      else
      {
        toolTip1.Show("There appears to be a problem communicating\nwith the BIM 360 Glue server!", labHidden, new Point(-100, -25));
      }

      ResponseTextBox.Text = tResponse.verboseResponse;
    }

    private void butValidateAPIKey_Click(object sender, EventArgs e)
    {
      if ((txt_api_key.Text == "") || (txt_api_secret.Text == ""))
      {
        MessageBox.Show("Please enter your API Key and API Secret to validate and get information about your key.", "BIM 360 Glue API Input Required", MessageBoxButtons.OK, MessageBoxIcon.Information);
        txt_user_id.Focus();
        return;
      }

      string timestamp = GetUNIXEpochTimestamp().ToString();
      string callArgs = "";
      callArgs += "&company_id=" + HttpUtility.UrlEncode(txt_company_id.Text);
      callArgs += "&api_key=" + HttpUtility.UrlEncode(txt_api_key.Text);
      callArgs += "&timestamp=" + HttpUtility.UrlEncode(timestamp);
      callArgs += "&sig=" + HttpUtility.UrlEncode(GenerateAPISignature(timestamp));
      if (chk_pretty.Checked)
      {
        callArgs += "&pretty=1";
      }
      if (chk_no_http_status.Checked)
      {
        callArgs += "&no_http_status=1";
      }

      glueResponse tResponse = MakeAPICall("system/v1/apikey", callArgs, "GET", "");
      if ((tResponse.statusCode == HttpStatusCode.OK) && (!chk_no_http_status.Checked))
      {
        if (tResponse.responseBody != "")
        {
          if (combo_response_format.Text == "json")
          {
            JavaScriptSerializer jSer = new JavaScriptSerializer();
            system_apikey_response_v1 tJSON = jSer.Deserialize<system_apikey_response_v1>(tResponse.responseBody);
            if (tJSON.service_provider_access == 1)
            {
              chk_service_provider.Checked = true;
            }
            else
            {
              chk_service_provider.Checked = false;
            }            
          }
          else if (combo_response_format.Text == "xml")
          {
            XmlSerializer mySerializer = new XmlSerializer(typeof(system_apikey_response_v1));
            system_apikey_response_v1 tXML = (system_apikey_response_v1)mySerializer.Deserialize((new StringReader(tResponse.responseBody)));
            if (tXML.service_provider_access == 1)
            {
              chk_service_provider.Checked = true;
            }
            else
            {
              chk_service_provider.Checked = false;
            }
          }

          toolTip1.Show("The API Key was successfully verfied!", txt_base_url, new Point(50, 80));
        }
      }

      ResponseTextBox.Text = tResponse.verboseResponse;
    }

    #endregion System Service Group

    //===============================================================
    // SECURITY SERVICE GROUP: Client Implementations
    //===============================================================
    #region Security Service Group
    private void LoginButton_Click_1(object sender, EventArgs e)
    {
      if ((txt_login_name.Text == "") || (txt_password.Text == ""))
      {
        MessageBox.Show("You must enter a login name and password for this service call", "BIM 360 Glue API Input Required", MessageBoxButtons.OK, MessageBoxIcon.Information);
        txt_auth_token.Focus();
        return;
      }

      string timestamp = GetUNIXEpochTimestamp().ToString();

      string postBody = "";
      postBody += "login_name=" + HttpUtility.UrlEncode(txt_login_name.Text);
      postBody += "&password=" + HttpUtility.UrlEncode(txt_password.Text);
      postBody += "&company_id=" + HttpUtility.UrlEncode(txt_company_id.Text);
      postBody += "&api_key=" + HttpUtility.UrlEncode(txt_api_key.Text);
      postBody += "&timestamp=" + HttpUtility.UrlEncode(timestamp);
      postBody += "&sig=" + HttpUtility.UrlEncode(GenerateAPISignature(timestamp));
      if (chk_pretty.Checked)
      {
        postBody += "&pretty=1";
      }

      if (chk_no_http_status.Checked)
      {
        postBody += "&no_http_status=1";
      }

      glueResponse tResponse = MakeAPICall("security/v1/login", "", "POST", postBody);
      if ((tResponse.statusCode == HttpStatusCode.OK) && (!chk_no_http_status.Checked))
      {
        if (tResponse.responseBody != "")
        {
          if (combo_response_format.Text == "json")
          {
            JavaScriptSerializer jSer = new JavaScriptSerializer();
            security_login_response_v1 tJSON = jSer.Deserialize<security_login_response_v1>(tResponse.responseBody);
            txt_auth_token.Text = tJSON.auth_token;
            txt_my_user_id.Text = tJSON.user_id;
          }
          else if (combo_response_format.Text == "xml")
          {
            XmlSerializer mySerializer = new XmlSerializer(typeof(security_login_response_v1));
            security_login_response_v1 tXML = (security_login_response_v1)mySerializer.Deserialize((new StringReader(tResponse.responseBody)));
            txt_auth_token.Text = tXML.auth_token;
            txt_my_user_id.Text = tXML.user_id;
          }
        }
        buttonLogin.Enabled = false;
        buttonLogout.Enabled = true;
        StoreSettings();
      }

      ResponseTextBox.Text = tResponse.verboseResponse;
    }

    private void buttonLogout_Click(object sender, EventArgs e)
    {
      if (txt_auth_token.Text == "")
      {
        MessageBox.Show("You must login and get an auth_token for this service call", "BIM 360 Glue API Input Required", MessageBoxButtons.OK, MessageBoxIcon.Information);
        txt_auth_token.Focus();
        return;
      }

      string timestamp = GetUNIXEpochTimestamp().ToString();

      string postBody = "";
      postBody += "&company_id=" + HttpUtility.UrlEncode(txt_company_id.Text);
      postBody += "&api_key=" + HttpUtility.UrlEncode(txt_api_key.Text);
      postBody += "&auth_token=" + HttpUtility.UrlEncode(txt_auth_token.Text);
      postBody += "&timestamp=" + HttpUtility.UrlEncode(timestamp);
      postBody += "&sig=" + HttpUtility.UrlEncode(GenerateAPISignature(timestamp));
      if (chk_pretty.Checked)
      {
        postBody += "&pretty=1";
      }
      if (chk_no_http_status.Checked)
      {
        postBody += "&no_http_status=1";
      }

      glueResponse tResponse = MakeAPICall("security/v1/logout", "", "POST", postBody);
      if ((tResponse.statusCode == HttpStatusCode.OK) && (!chk_no_http_status.Checked))
      {
      }

      buttonLogin.Enabled = true;
      buttonLogout.Enabled = false;
      txt_auth_token.Text = "";
      txt_my_user_id.Text = "";
      ResponseTextBox.Text = tResponse.verboseResponse;
      StoreSettings();
    }

    private void butProxyLogin_Click(object sender, EventArgs e)
    {
      if (txt_login_name.Text == "")
      {
        MessageBox.Show("You must enter a login name for this service call", "BIM 360 Glue API Input Required", MessageBoxButtons.OK, MessageBoxIcon.Information);
        txt_auth_token.Focus();
        return;
      }

      string timestamp = GetUNIXEpochTimestamp().ToString();

      string postBody = "";
      postBody += "proxy_login_name=" + HttpUtility.UrlEncode(txt_login_name.Text);
      postBody += "&password=" + HttpUtility.UrlEncode(txt_password.Text);
      postBody += "&company_id=" + HttpUtility.UrlEncode(txt_company_id.Text);
      postBody += "&api_key=" + HttpUtility.UrlEncode(txt_api_key.Text);
      postBody += "&timestamp=" + HttpUtility.UrlEncode(timestamp);
      postBody += "&sig=" + HttpUtility.UrlEncode(GenerateAPISignature(timestamp));
      if (chk_pretty.Checked)
      {
        postBody += "&pretty=1";
      }

      if (chk_no_http_status.Checked)
      {
        postBody += "&no_http_status=1";
      }

      glueResponse tResponse = MakeAPICall("security/v1/proxylogin", "", "POST", postBody);
      if ((tResponse.statusCode == HttpStatusCode.OK) && (!chk_no_http_status.Checked))
      {
        if (tResponse.responseBody != "")
        {
          if (combo_response_format.Text == "json")
          {
            JavaScriptSerializer jSer = new JavaScriptSerializer();
            security_login_response_v1 tJSON = jSer.Deserialize<security_login_response_v1>(tResponse.responseBody);
            txt_auth_token.Text = tJSON.auth_token;
            txt_my_user_id.Text = tJSON.user_id;
          }
          else if (combo_response_format.Text == "xml")
          {
            XmlSerializer mySerializer = new XmlSerializer(typeof(security_login_response_v1));
            security_login_response_v1 tXML = (security_login_response_v1)mySerializer.Deserialize((new StringReader(tResponse.responseBody)));
            txt_auth_token.Text = tXML.auth_token;
            txt_my_user_id.Text = tXML.user_id;
          }
        }
        buttonLogin.Enabled = false;
        buttonLogout.Enabled = true;
        StoreSettings();
      }

      ResponseTextBox.Text = tResponse.verboseResponse;
    }

    private void butProxyLogout_Click(object sender, EventArgs e)
    {
      if (txt_auth_token.Text == "")
      {
        MessageBox.Show("You must login and get an auth_token for this service call", "BIM 360 Glue API Input Required", MessageBoxButtons.OK, MessageBoxIcon.Information);
        txt_auth_token.Focus();
        return;
      }

      string timestamp = GetUNIXEpochTimestamp().ToString();

      string postBody = "";
      postBody += "&company_id=" + HttpUtility.UrlEncode(txt_company_id.Text);
      postBody += "&api_key=" + HttpUtility.UrlEncode(txt_api_key.Text);
      postBody += "&auth_token=" + HttpUtility.UrlEncode(txt_auth_token.Text);
      postBody += "&timestamp=" + HttpUtility.UrlEncode(timestamp);
      postBody += "&sig=" + HttpUtility.UrlEncode(GenerateAPISignature(timestamp));
      if (chk_pretty.Checked)
      {
        postBody += "&pretty=1";
      }
      if (chk_no_http_status.Checked)
      {
        postBody += "&no_http_status=1";
      }

      glueResponse tResponse = MakeAPICall("security/v1/proxylogout", "", "POST", postBody);
      if ((tResponse.statusCode == HttpStatusCode.OK) && (!chk_no_http_status.Checked))
      {
      }

      buttonLogin.Enabled = true;
      buttonLogout.Enabled = false;
      txt_auth_token.Text = "";
      txt_my_user_id.Text = "";
      ResponseTextBox.Text = tResponse.verboseResponse;
      StoreSettings();
    }

    private void butEnableUser_Click_1(object sender, EventArgs e)
    {
      // if (txt_user_id.Text == "")
      // {
      //   MessageBox.Show("You must select a user_id for this service call. Use the \"Find User\" Section to the left to lookup a user for this operation.", "BIM 360 Glue API Input Required", MessageBoxButtons.OK, MessageBoxIcon.Information);
      //   txt_user_id.Focus();
      //   return;
      // }

      string timestamp = GetUNIXEpochTimestamp().ToString();
      string postBody = "";
      postBody += "&company_id=" + HttpUtility.UrlEncode(txt_company_id.Text);
      postBody += "&api_key=" + HttpUtility.UrlEncode(txt_api_key.Text);
      postBody += "&auth_token=" + HttpUtility.UrlEncode(txt_auth_token.Text);
      postBody += "&timestamp=" + HttpUtility.UrlEncode(timestamp);
      postBody += "&sig=" + HttpUtility.UrlEncode(GenerateAPISignature(timestamp));
      //      postBody += "&proxy_user_id=" + HttpUtility.UrlEncode(txt_user_id.Text);
      postBody += "&proxy_login_name=" + HttpUtility.UrlEncode(txt_sp_login_name.Text);

      if (chk_pretty.Checked)
      {
        postBody += "&pretty=1";
      }
      if (chk_no_http_status.Checked)
      {
        postBody += "&no_http_status=1";
      }

      glueResponse tResponse = MakeAPICall("security/v1/enable", "", "POST", postBody);
      if ((tResponse.statusCode == HttpStatusCode.OK) && (!chk_no_http_status.Checked))
      {
      }

      ResponseTextBox.Text = tResponse.verboseResponse;
    }

    private void butDisableUserAccount_Click(object sender, EventArgs e)
    {
      // if (txt_user_id.Text == "")
      // {
      //   MessageBox.Show("You must select a user_id for this service call.  Use the \"Find User\" Section to the left to lookup a user for this operation.", "BIM 360 Glue API Input Required", MessageBoxButtons.OK, MessageBoxIcon.Information);
      //   txt_user_id.Focus();
      //   return;
      // }

      string timestamp = GetUNIXEpochTimestamp().ToString();
      string postBody = "";
      postBody += "&company_id=" + HttpUtility.UrlEncode(txt_company_id.Text);
      postBody += "&api_key=" + HttpUtility.UrlEncode(txt_api_key.Text);
      postBody += "&auth_token=" + HttpUtility.UrlEncode(txt_auth_token.Text);
      postBody += "&timestamp=" + HttpUtility.UrlEncode(timestamp);
      postBody += "&sig=" + HttpUtility.UrlEncode(GenerateAPISignature(timestamp));
      //      postBody += "&proxy_user_id=" + HttpUtility.UrlEncode(txt_user_id.Text);
      postBody += "&proxy_login_name=" + HttpUtility.UrlEncode(txt_sp_login_name.Text);
      if (chk_pretty.Checked)
      {
        postBody += "&pretty=1";
      }
      if (chk_no_http_status.Checked)
      {
        postBody += "&no_http_status=1";
      }

      glueResponse tResponse = MakeAPICall("security/v1/disable", "", "POST", postBody);
      if ((tResponse.statusCode == HttpStatusCode.OK) && (!chk_no_http_status.Checked))
      {
      }

      ResponseTextBox.Text = tResponse.verboseResponse;
    }

    private void deleteUserButton_Click(object sender, EventArgs e)
    {
      string timestamp = GetUNIXEpochTimestamp().ToString();
      string postBody = "";
      postBody += "&company_id=" + HttpUtility.UrlEncode(txt_company_id.Text);
      postBody += "&api_key=" + HttpUtility.UrlEncode(txt_api_key.Text);
      postBody += "&auth_token=" + HttpUtility.UrlEncode(txt_auth_token.Text);
      postBody += "&timestamp=" + HttpUtility.UrlEncode(timestamp);
      postBody += "&sig=" + HttpUtility.UrlEncode(GenerateAPISignature(timestamp));
      //      postBody += "&proxy_user_id=" + HttpUtility.UrlEncode(txt_user_id.Text);
      postBody += "&proxy_login_name=" + HttpUtility.UrlEncode(txt_sp_login_name.Text);
      if (chk_pretty.Checked)
      {
        postBody += "&pretty=1";
      }
      if (chk_no_http_status.Checked)
      {
        postBody += "&no_http_status=1";
      }

      glueResponse tResponse = MakeAPICall("security/v1/delete", "", "POST", postBody);
      if ((tResponse.statusCode == HttpStatusCode.OK) && (!chk_no_http_status.Checked))
      {
      }

      ResponseTextBox.Text = tResponse.verboseResponse;
    }

    private void buttonForcePasswordChange_Click_1(object sender, EventArgs e)
    {
      // if (txt_user_id.Text == "")
      // {
      //   MessageBox.Show("You must select a user_id for this service call. Use the \"Find User\" Section to the left to lookup a user for this operation.", "BIM 360 Glue API Input Required", MessageBoxButtons.OK, MessageBoxIcon.Information);
      //   txt_user_id.Focus();
      //   return;
      // }

      string timestamp = GetUNIXEpochTimestamp().ToString();
      string postBody = "";
      postBody += "&company_id=" + HttpUtility.UrlEncode(txt_company_id.Text);
      postBody += "&api_key=" + HttpUtility.UrlEncode(txt_api_key.Text);
      postBody += "&auth_token=" + HttpUtility.UrlEncode(txt_auth_token.Text);
      postBody += "&timestamp=" + HttpUtility.UrlEncode(timestamp);
      postBody += "&sig=" + HttpUtility.UrlEncode(GenerateAPISignature(timestamp));
      //      postBody += "&proxy_user_id=" + HttpUtility.UrlEncode(txt_user_id.Text);
      postBody += "&proxy_login_name=" + HttpUtility.UrlEncode(txt_sp_login_name.Text);
      if (chk_pretty.Checked)
      {
        postBody += "&pretty=1";
      }
      if (chk_no_http_status.Checked)
      {
        postBody += "&no_http_status=1";
      }

      glueResponse tResponse = MakeAPICall("security/v1/password/changeflag", "", "POST", postBody);
      if ((tResponse.statusCode == HttpStatusCode.OK) && (!chk_no_http_status.Checked))
      {
      }

      ResponseTextBox.Text = tResponse.verboseResponse;
    }

    private void butCreateNewAccount_Click(object sender, EventArgs e)
    {
      string timestamp = GetUNIXEpochTimestamp().ToString();
      string postBody = "";
      postBody += "&company_id=" + HttpUtility.UrlEncode(txt_company_id.Text);
      postBody += "&api_key=" + HttpUtility.UrlEncode(txt_api_key.Text);
      postBody += "&timestamp=" + HttpUtility.UrlEncode(timestamp);
      postBody += "&sig=" + HttpUtility.UrlEncode(GenerateAPISignature(timestamp));
      postBody += "&login_name=" + HttpUtility.UrlEncode(txt_create_login_name.Text);
      postBody += "&password=" + HttpUtility.UrlEncode(txt_create_password.Text);
      postBody += "&email=" + HttpUtility.UrlEncode(txt_create_email.Text);
      if (chk_pretty.Checked)
      {
        postBody += "&pretty=1";
      }
      if (chk_no_http_status.Checked)
      {
        postBody += "&no_http_status=1";
      }

      glueResponse tResponse = MakeAPICall("security/v1/create", "", "POST", postBody);
      if ((tResponse.statusCode == HttpStatusCode.OK) && (!chk_no_http_status.Checked))
      {
      }

      ResponseTextBox.Text = tResponse.verboseResponse;
    }

    private void butAddAutodeskIDUser_Click(object sender, EventArgs e)
    {
      string timestamp = GetUNIXEpochTimestamp().ToString();
      string postBody = "";
      postBody += "&company_id=" + HttpUtility.UrlEncode(txt_company_id.Text);
      postBody += "&api_key=" + HttpUtility.UrlEncode(txt_api_key.Text);
      postBody += "&timestamp=" + HttpUtility.UrlEncode(timestamp);
      postBody += "&sig=" + HttpUtility.UrlEncode(GenerateAPISignature(timestamp));
      postBody += "&username=" + HttpUtility.UrlEncode(txt_create_login_name.Text);
      postBody += "&password=" + HttpUtility.UrlEncode(txt_create_password.Text);
      if (chk_pretty.Checked)
      {
        postBody += "&pretty=1";
      }
      if (chk_no_http_status.Checked)
      {
        postBody += "&no_http_status=1";
      }

      glueResponse tResponse = MakeAPICall("security/v1/add", "", "POST", postBody);
      if ((tResponse.statusCode == HttpStatusCode.OK) && (!chk_no_http_status.Checked))
      {
      }

      ResponseTextBox.Text = tResponse.verboseResponse;
    }

    #endregion Security Service Group

    //===============================================================
    // USER SERVICE GROUP: Client Implementations
    //===============================================================
    #region User Service Group

    private void butGetCompanies_Click(object sender, EventArgs e)
    {
      string timestamp = GetUNIXEpochTimestamp().ToString();
      string callArgs = "";
      callArgs += "&company_id=" + HttpUtility.UrlEncode(txt_company_id.Text);
      callArgs += "&api_key=" + HttpUtility.UrlEncode(txt_api_key.Text);
      callArgs += "&auth_token=" + HttpUtility.UrlEncode(txt_auth_token.Text);
      callArgs += "&timestamp=" + HttpUtility.UrlEncode(timestamp);
      callArgs += "&sig=" + HttpUtility.UrlEncode(GenerateAPISignature(timestamp));
      callArgs += "&login_name=" + HttpUtility.UrlEncode(txt_enter_login_name.Text);

      if (chk_pretty.Checked)
      {
        callArgs += "&pretty=1";
      }
      if (chk_no_http_status.Checked)
      {
        callArgs += "&no_http_status=1";
      }

      glueResponse tResponse = MakeAPICall("user/v1/companies", callArgs, "GET", "");
      if ((tResponse.statusCode == HttpStatusCode.OK) && (!chk_no_http_status.Checked))
      {

        if (tResponse.responseBody != "")
        {
          if (combo_response_format.Text == "json")
          {
            JavaScriptSerializer jSer = new JavaScriptSerializer();
            user_company_list tJSON = jSer.Deserialize<user_company_list>(tResponse.responseBody);
          }
          else if (combo_response_format.Text == "xml")
          {
            XmlSerializer mySerializer = new XmlSerializer(typeof(user_info_response_v1));
            user_company_list tXML = (user_company_list)mySerializer.Deserialize((new StringReader(tResponse.responseBody)));
          }
        }
      }

      ResponseTextBox.Text = tResponse.verboseResponse;
    }

    private void butChangePassword_Click_1(object sender, EventArgs e)
    {
      if (txt_new_password.Text == "")
      {
        MessageBox.Show("You must enter the new password for this service call", "BIM 360 Glue API Input Required", MessageBoxButtons.OK, MessageBoxIcon.Information);
        txt_new_password.Focus();
        return;
      }

      string timestamp = GetUNIXEpochTimestamp().ToString();
      string postBody = "";
      postBody += "&company_id=" + HttpUtility.UrlEncode(txt_company_id.Text);
      postBody += "&api_key=" + HttpUtility.UrlEncode(txt_api_key.Text);
      postBody += "&auth_token=" + HttpUtility.UrlEncode(txt_auth_token.Text);
      postBody += "&timestamp=" + HttpUtility.UrlEncode(timestamp);
      postBody += "&sig=" + HttpUtility.UrlEncode(GenerateAPISignature(timestamp));
      postBody += "&proxy_login_name=" + HttpUtility.UrlEncode(txt_sp_login_name.Text);

      postBody += "&old_password=" + HttpUtility.UrlEncode(txt_old_password.Text);
      postBody += "&new_password=" + HttpUtility.UrlEncode(txt_new_password.Text);

      if (chk_pretty.Checked)
      {
        postBody += "&pretty=1";
      }
      if (chk_no_http_status.Checked)
      {
        postBody += "&no_http_status=1";
      }

      glueResponse tResponse = MakeAPICall("user/v1/password", "", "POST", postBody);
      if ((tResponse.statusCode == HttpStatusCode.OK) && (!chk_no_http_status.Checked))
      {
      }

      ResponseTextBox.Text = tResponse.verboseResponse;
    }

    private void butUserInformation_Click_1(object sender, EventArgs e)
    {
      string timestamp = GetUNIXEpochTimestamp().ToString();
      string callArgs = "";
      callArgs += "&company_id=" + HttpUtility.UrlEncode(txt_company_id.Text);
      callArgs += "&api_key=" + HttpUtility.UrlEncode(txt_api_key.Text);
      callArgs += "&auth_token=" + HttpUtility.UrlEncode(txt_auth_token.Text);
      callArgs += "&timestamp=" + HttpUtility.UrlEncode(timestamp);
      callArgs += "&sig=" + HttpUtility.UrlEncode(GenerateAPISignature(timestamp));
      callArgs += "&login_name=" + HttpUtility.UrlEncode(txt_enter_login_name.Text);

      if (chk_pretty.Checked)
      {
        callArgs += "&pretty=1";
      }
      if (chk_no_http_status.Checked)
      {
        callArgs += "&no_http_status=1";
      }

      glueResponse tResponse = MakeAPICall("user/v1/info", callArgs, "GET", "");
      if ((tResponse.statusCode == HttpStatusCode.OK) && (!chk_no_http_status.Checked))
      {
        
        if (tResponse.responseBody != "")
        {
          if (combo_response_format.Text == "json")
          {
            JavaScriptSerializer jSer = new JavaScriptSerializer();
            user_info_response_v1 tJSON = jSer.Deserialize<user_info_response_v1>(tResponse.responseBody);
            txt_user_id.Text = tJSON.user_id;
            txt_login_name_display.Text = tJSON.login_name;
          }
          else if (combo_response_format.Text == "xml")
          {
            XmlSerializer mySerializer = new XmlSerializer(typeof(user_info_response_v1));
            user_info_response_v1 tXML = (user_info_response_v1)mySerializer.Deserialize((new StringReader(tResponse.responseBody)));
            txt_user_id.Text = tXML.user_id;
            txt_login_name_display.Text = tXML.login_name;
          }
        }
      }

      ResponseTextBox.Text = tResponse.verboseResponse;
    }


    private void UserListView_ItemClick(Object sender, ListViewItemSelectionChangedEventArgs e)
    {
      if (e.IsSelected)
      {
        txt_enter_login_name.Text = this.lvUsers.Items[e.ItemIndex].SubItems[0].Text;
        txt_sp_login_name.Text = this.lvUsers.Items[e.ItemIndex].SubItems[0].Text;
      }
    }

    private void butCompanyRoster_Click(object sender, EventArgs e)
    {
      string timestamp = GetUNIXEpochTimestamp().ToString();
      string callArgs = "";
      callArgs += "&company_id=" + HttpUtility.UrlEncode(txt_company_id.Text);
      callArgs += "&api_key=" + HttpUtility.UrlEncode(txt_api_key.Text);
      callArgs += "&auth_token=" + HttpUtility.UrlEncode(txt_auth_token.Text);
      callArgs += "&timestamp=" + HttpUtility.UrlEncode(timestamp);
      callArgs += "&sig=" + HttpUtility.UrlEncode(GenerateAPISignature(timestamp));
      callArgs += "&sterm=" + HttpUtility.UrlEncode(txt_user_search_term.Text);
      callArgs += "&page=" + num_user_page.Value.ToString();
      callArgs += "&page_size=" + num_user_page_size.Value.ToString();

      if (chk_pretty.Checked)
      {
        callArgs += "&pretty=1";
      }
      if (chk_no_http_status.Checked)
      {
        callArgs += "&no_http_status=1";
      }

      glueResponse tResponse = MakeAPICall("user/v1/company_roster_ex", callArgs, "GET", "");
      if ((tResponse.statusCode == HttpStatusCode.OK) && (!chk_no_http_status.Checked))
      {
        if (tResponse.responseBody != "")
        {
          user_info_response_v1[] tRoster = null;

          if (combo_response_format.Text == "json")
          {
            JavaScriptSerializer jSer = new JavaScriptSerializer();
            user_company_roster_response_v1 tJSON = jSer.Deserialize<user_company_roster_response_v1>(tResponse.responseBody);
            if (tJSON.user_roster != null)
            {
              tRoster = tJSON.user_roster;
            }
          }
          else if (combo_response_format.Text == "xml")
          {
            XmlSerializer mySerializer = new XmlSerializer(typeof(user_company_roster_response_v1));
            user_company_roster_response_v1 tXML = (user_company_roster_response_v1)mySerializer.Deserialize((new StringReader(tResponse.responseBody)));

            if (tXML.user_roster != null)
            {
              tRoster = tXML.user_roster;
            }
          }

          lvUsers.Items.Clear();
          if (tRoster != null)
          {
            foreach (user_info_response_v1 userInfo in tRoster)
            {
              ListViewItem newItem = new ListViewItem(userInfo.login_name); //Parent item
              ListViewItem.ListViewSubItem aSubItem1 = new ListViewItem.ListViewSubItem(newItem, userInfo.created_date); //Creating subitems for the parent item
              ListViewItem.ListViewSubItem aSubItem2 = new ListViewItem.ListViewSubItem(newItem, userInfo.user_id);
              newItem.SubItems.Add(aSubItem1); //Associating these subitems to the parent item
              newItem.SubItems.Add(aSubItem2);
              lvUsers.Items.Add(newItem); //Adding the parent item to the listview control
            }
          }
        }
      }

      ResponseTextBox.Text = tResponse.verboseResponse;
    }

    private void DoProfileSettingsUpdate()
    {
      string timestamp = GetUNIXEpochTimestamp().ToString();
      string postBody = "";
      postBody += "&company_id=" + HttpUtility.UrlEncode(txt_company_id.Text);
      postBody += "&api_key=" + HttpUtility.UrlEncode(txt_api_key.Text);
      postBody += "&auth_token=" + HttpUtility.UrlEncode(txt_auth_token.Text);
      postBody += "&timestamp=" + HttpUtility.UrlEncode(timestamp);
      postBody += "&sig=" + HttpUtility.UrlEncode(GenerateAPISignature(timestamp));
      postBody += "&proxy_login_name=" + HttpUtility.UrlEncode(txt_sp_login_name.Text);

      postBody += "&new_login_name=" + HttpUtility.UrlEncode(txt_new_login_name.Text);
      postBody += "&new_email=" + HttpUtility.UrlEncode(txt_new_email.Text);

      if (chk_pretty.Checked)
      {
        postBody += "&pretty=1";
      }
      if (chk_no_http_status.Checked)
      {
        postBody += "&no_http_status=1";
      }

      glueResponse tResponse = MakeAPICall("user/v1/profile", "", "POST", postBody);
      if ((tResponse.statusCode == HttpStatusCode.OK) && (!chk_no_http_status.Checked))
      {
      }

      ResponseTextBox.Text = tResponse.verboseResponse;
    }

    private void butNewEmail_Click(object sender, EventArgs e)
    {
      DoProfileSettingsUpdate();
    }

    private void butNewLoginName_Click(object sender, EventArgs e)
    {
      DoProfileSettingsUpdate();
    }

    #endregion User Service Group

    #region General Access Routines
    //----------------------------------------------------------------------------
    // General Routines for Access
    //----------------------------------------------------------------------------
    private void ShowProjectList(ListView aListView, string searchTerm, int page, int page_size)
    {
      string timestamp = GetUNIXEpochTimestamp().ToString();
      string callArgs = "";
      callArgs += "&company_id=" + HttpUtility.UrlEncode(txt_company_id.Text);
      callArgs += "&api_key=" + HttpUtility.UrlEncode(txt_api_key.Text);
      callArgs += "&auth_token=" + HttpUtility.UrlEncode(txt_auth_token.Text);
      callArgs += "&timestamp=" + HttpUtility.UrlEncode(timestamp);
      callArgs += "&sig=" + HttpUtility.UrlEncode(GenerateAPISignature(timestamp));
      callArgs += "&sterm=" + HttpUtility.UrlEncode(searchTerm);
      callArgs += "&page=" + page.ToString();
      callArgs += "&page_size=" + page_size.ToString();

      if (chk_pretty.Checked)
      {
        callArgs += "&pretty=1";
      }
      if (chk_no_http_status.Checked)
      {
        callArgs += "&no_http_status=1";
      }

      glueResponse tResponse = MakeAPICall("project/v1/list", callArgs, "GET", "");
      if ((tResponse.statusCode == HttpStatusCode.OK) && (!chk_no_http_status.Checked))
      {
        if (tResponse.responseBody != "")
        {
          project_info_response_v1[] tProjects = null;
          if (combo_response_format.Text == "json")
          {
            JavaScriptSerializer jSer = new JavaScriptSerializer();
            project_list_response_v1 tJSON = jSer.Deserialize<project_list_response_v1>(tResponse.responseBody);
            if (tJSON.project_list != null)
            {
              tProjects = tJSON.project_list;
            }
          }
          else if (combo_response_format.Text == "xml")
          {
            XmlSerializer mySerializer = new XmlSerializer(typeof(project_list_response_v1));
            project_list_response_v1 tXML = (project_list_response_v1)mySerializer.Deserialize((new StringReader(tResponse.responseBody)));

            if (tXML.project_list != null)
            {
              tProjects = tXML.project_list;
            }
          }

          aListView.Items.Clear();
          if (tProjects != null)
          {
            foreach (project_info_response_v1 projectInfo in tProjects)
            {
              ListViewItem newItem = new ListViewItem(HttpUtility.UrlDecode(projectInfo.project_name)); //Parent item
              ListViewItem.ListViewSubItem aSubItem1 = new ListViewItem.ListViewSubItem(newItem, projectInfo.created_date); //Creating subitems for the parent item
              ListViewItem.ListViewSubItem aSubItem2 = new ListViewItem.ListViewSubItem(newItem, projectInfo.project_id);
              newItem.SubItems.Add(aSubItem1); //Associating these subitems to the parent item
              newItem.SubItems.Add(aSubItem2);
              aListView.Items.Add(newItem); //Adding the parent item to the listview control
            }
          }
        }
      }

      ResponseTextBox.Text = tResponse.verboseResponse;
    }

    private void ProjectRosterListView_ItemClick(Object sender, ListViewItemSelectionChangedEventArgs e)
    {
      if (e.IsSelected)
      {
        txt_new_project_user.Text = this.lvProjectRoster.Items[e.ItemIndex].SubItems[0].Text;
      }
    }

    private void ShowProjectInfo(string projectID)
    {
      string timestamp = GetUNIXEpochTimestamp().ToString();
      string callArgs = "";
      callArgs += "&company_id=" + HttpUtility.UrlEncode(txt_company_id.Text);
      callArgs += "&api_key=" + HttpUtility.UrlEncode(txt_api_key.Text);
      callArgs += "&auth_token=" + HttpUtility.UrlEncode(txt_auth_token.Text);
      callArgs += "&timestamp=" + HttpUtility.UrlEncode(timestamp);
      callArgs += "&sig=" + HttpUtility.UrlEncode(GenerateAPISignature(timestamp));
      callArgs += "&sterm=" + HttpUtility.UrlEncode(txt_project_search_term.Text);
      callArgs += "&project_id=" + projectID;

      if (chk_lightweight.Checked)
      {
        callArgs += "&lightweight=1";
      }

      if (numDepth.Value > 0)
      {
        callArgs += "&depth=" + numDepth.Value.ToString();
      }

      if (chk_pretty.Checked)
      {
        callArgs += "&pretty=1";
      }
      if (chk_no_http_status.Checked)
      {
        callArgs += "&no_http_status=1";
      }

      glueResponse tResponse = MakeAPICall("project/v1/info", callArgs, "GET", "");
      glueResponse tResponse1 = MakeAPICall("project/v1/users", callArgs, "GET", "");
      if ((tResponse.statusCode == HttpStatusCode.OK) && (!chk_no_http_status.Checked))
      {
        if (tResponse.responseBody != "")
        {
          glue_folder_node[] tTreeNodes = null;
          user_info_response_v1[] project_roster = null;
          if (combo_response_format.Text == "json")
          {
            JavaScriptSerializer jSer = new JavaScriptSerializer();
            project_info_response_v1 tJSON = jSer.Deserialize<project_info_response_v1>(tResponse.responseBody);

            JavaScriptSerializer jSer1 = new JavaScriptSerializer();
            project_info_response_v1 tJSON1 = jSer1.Deserialize<project_info_response_v1>(tResponse1.responseBody);
            if (tJSON.folder_tree != null)
            {
              tTreeNodes = tJSON.folder_tree;
            }
            project_roster = tJSON1.project_roster;
          }
          else if (combo_response_format.Text == "xml")
          {
            XmlSerializer mySerializer = new XmlSerializer(typeof(project_info_response_v1));
            project_info_response_v1 tXML = (project_info_response_v1)mySerializer.Deserialize((new StringReader(tResponse.responseBody)));

            if (tXML.folder_tree != null)
            {
              tTreeNodes = tXML.folder_tree;
            }
            project_roster = tXML.project_roster;
          }

          lvProjectClashReports.Items.Clear(); 
          tvProject.Nodes.Clear();
          if (tTreeNodes != null)
          {
            RenderTreeNode(tTreeNodes);
          }

          // Now load the project roster...
          lvProjectRoster.Items.Clear();
          if (project_roster != null)
          {
            foreach (user_info_response_v1 nodeInfo in project_roster)
            {
              ListViewItem newItem = new ListViewItem(HttpUtility.UrlDecode(nodeInfo.login_name)); //Parent item
              ListViewItem.ListViewSubItem aSubItem1 = new ListViewItem.ListViewSubItem(newItem, nodeInfo.user_id); //Creating subitems for the parent item
              newItem.SubItems.Add(aSubItem1); //Associating these subitems to the parent item
              lvProjectRoster.Items.Add(newItem); //Adding the parent item to the listview control
            }
          }

          if (tvProject.Nodes.Count > 0)
          {
            tvProject.ExpandAll();
            tvProject.SelectedNode = tvProject.Nodes[0];
          }
        }
      }

      ResponseTextBox.Text = tResponse1.verboseResponse;
    }

    #endregion General Access Routines

    //===============================================================
    // PROJECT SERVICE GROUP: Client Implementations
    //===============================================================
    #region Project Service Group

    private void butGetProjectWithDepth_Click(object sender, EventArgs e)
    {
      ShowProjectInfo(txt_project_id.Text);
    }

    private void butGetPrunedTree_Click(object sender, EventArgs e)
    {
      ListView aListView = lvModelModels;
      string projectID = txt_project_id.Text;
      string timestamp = GetUNIXEpochTimestamp().ToString();
      string callArgs = "";
      callArgs += "&company_id=" + HttpUtility.UrlEncode(txt_company_id.Text);
      callArgs += "&api_key=" + HttpUtility.UrlEncode(txt_api_key.Text);
      callArgs += "&auth_token=" + HttpUtility.UrlEncode(txt_auth_token.Text);
      callArgs += "&timestamp=" + HttpUtility.UrlEncode(timestamp);
      callArgs += "&sig=" + HttpUtility.UrlEncode(GenerateAPISignature(timestamp));
      callArgs += "&sterm=" + HttpUtility.UrlEncode(txt_project_search_term.Text);
      callArgs += "&project_id=" + projectID;
      callArgs += "&folder_id=" + txt_project_object_id.Text;
      callArgs += "&depth=" + numDepth.Value;

      if (chk_lightweight.Checked)
      {
        callArgs += "&lightweight=1";
      }

      if (chk_pretty.Checked)
      {
        callArgs += "&pretty=1";
      }
      if (chk_no_http_status.Checked)
      {
        callArgs += "&no_http_status=1";
      }

      glueResponse tResponse = MakeAPICall("project/v1/tree", callArgs, "GET", "");
      if ((tResponse.statusCode == HttpStatusCode.OK) && (!chk_no_http_status.Checked))
      {
        if (tResponse.responseBody != "")
        {
          glue_folder_node[] tTreeNodes = null;
          if (combo_response_format.Text == "json")
          {
            JavaScriptSerializer jSer = new JavaScriptSerializer();
            project_info_response_v1 tJSON = jSer.Deserialize<project_info_response_v1>(tResponse.responseBody);
            if (tJSON.folder_tree != null)
            {
              tTreeNodes = tJSON.folder_tree;
            }
          }
          else if (combo_response_format.Text == "xml")
          {
            XmlSerializer mySerializer = new XmlSerializer(typeof(project_info_response_v1));
            project_info_response_v1 tXML = (project_info_response_v1)mySerializer.Deserialize((new StringReader(tResponse.responseBody)));

            if (tXML.folder_tree != null)
            {
              tTreeNodes = tXML.folder_tree;
            }
          }

          tvProject.Nodes.Clear();
          if (tTreeNodes != null)
          {
            RenderTreeNode(tTreeNodes);
          }
        }
      }

      ResponseTextBox.Text = tResponse.responseBody;
    }

    private void ProjectListView_ItemClick(Object sender, ListViewItemSelectionChangedEventArgs e)
    {
      if (e.IsSelected)
      {
        txt_project_id.Text = this.lvProjects.Items[e.ItemIndex].SubItems[2].Text;
        ShowProjectInfo(txt_project_id.Text);
      }
    }

    private void butGetProjectList_Click(object sender, EventArgs e)
    {
      ShowProjectList(lvProjects, txt_project_search_term.Text, Convert.ToInt32(num_project_page.Value), Convert.ToInt32(num_project_page_size.Value));
    }

    void RenderTreeNode(glue_folder_node[] tTreeNodes, TreeNode parentNode = null)
    {
      foreach (glue_folder_node nodeInfo in tTreeNodes)
      {
        if (nodeInfo.type == "FOLDER")
        {
          TreeNode node = null;
          if (parentNode != null)
          {
            node = parentNode.Nodes.Add(nodeInfo.object_id, HttpUtility.UrlDecode(nodeInfo.name), 0, 0);
          }
          else
          {
            node = tvProject.Nodes.Add(nodeInfo.object_id, HttpUtility.UrlDecode(nodeInfo.name), 0, 0);
          }

          // Tag it...
          node.Tag = "FOLDER;" + nodeInfo.object_id + ";" + nodeInfo.version_id + ";" + nodeInfo.version + ";" + nodeInfo.name;

          if (nodeInfo.folder_contents != null)
          {
            RenderTreeNode(nodeInfo.folder_contents, node);
          }
        }
        else
        {
          TreeNode node = null;
          if (parentNode != null)
          {
            node = parentNode.Nodes.Add(nodeInfo.object_id, HttpUtility.UrlDecode(nodeInfo.name), 1, 1);
          }
          else
          {
            node = tvProject.Nodes.Add(nodeInfo.object_id, HttpUtility.UrlDecode(nodeInfo.name), 1, 1);
          }

          // Tag it...
          node.Tag = "MODEL;" + nodeInfo.object_id + ";" + nodeInfo.version_id + ";" + nodeInfo.version + ";" + nodeInfo.name;
        }
      }
    }

    private void butAddProject_Click(object sender, EventArgs e)
    {
      string timestamp = GetUNIXEpochTimestamp().ToString();
      string postBody = "";
      postBody += "&company_id=" + HttpUtility.UrlEncode(txt_company_id.Text);
      postBody += "&api_key=" + HttpUtility.UrlEncode(txt_api_key.Text);
      postBody += "&auth_token=" + HttpUtility.UrlEncode(txt_auth_token.Text);
      postBody += "&timestamp=" + HttpUtility.UrlEncode(timestamp);
      postBody += "&sig=" + HttpUtility.UrlEncode(GenerateAPISignature(timestamp));
      postBody += "&sterm=" + HttpUtility.UrlEncode(txt_project_search_term.Text);
      postBody += "&project_name=" + txt_new_project_name.Text;

      if (chk_pretty.Checked)
      {
        postBody += "&pretty=1";
      }
      if (chk_no_http_status.Checked)
      {
        postBody += "&no_http_status=1";
      }

      glueResponse tResponse = MakeAPICall("project/v1/create", "", "POST", postBody);
      if ((tResponse.statusCode == HttpStatusCode.OK) && (!chk_no_http_status.Checked))
      {
        if (tResponse.responseBody != "")
        {
          glue_folder_node[] tTreeNodes = null;
          if (combo_response_format.Text == "json")
          {
            JavaScriptSerializer jSer = new JavaScriptSerializer();
            project_info_response_v1 tJSON = jSer.Deserialize<project_info_response_v1>(tResponse.responseBody);
            if (tJSON.folder_tree != null)
            {
              tTreeNodes = tJSON.folder_tree;
            }
          }
          else if (combo_response_format.Text == "xml")
          {
            XmlSerializer mySerializer = new XmlSerializer(typeof(project_info_response_v1));
            project_info_response_v1 tXML = (project_info_response_v1)mySerializer.Deserialize((new StringReader(tResponse.responseBody)));

            if (tXML.folder_tree != null)
            {
              tTreeNodes = tXML.folder_tree;
            }
          }

          if (tTreeNodes != null)
          {
            RenderTreeNode(tTreeNodes);
          }
        }

        // refresh project list
        butGetProjectList_Click(sender, e);
      }

      ResponseTextBox.Text = tResponse.verboseResponse;
    }


    private void butDeleteProject_Click(object sender, EventArgs e)
    {
      string timestamp = GetUNIXEpochTimestamp().ToString();
      string postBody = "";
      postBody += "&company_id=" + HttpUtility.UrlEncode(txt_company_id.Text);
      postBody += "&api_key=" + HttpUtility.UrlEncode(txt_api_key.Text);
      postBody += "&auth_token=" + HttpUtility.UrlEncode(txt_auth_token.Text);
      postBody += "&timestamp=" + HttpUtility.UrlEncode(timestamp);
      postBody += "&sig=" + HttpUtility.UrlEncode(GenerateAPISignature(timestamp));
      postBody += "&sterm=" + HttpUtility.UrlEncode(txt_project_search_term.Text);
      postBody += "&project_id=" + txt_project_id.Text;

      if (chk_pretty.Checked)
      {
        postBody += "&pretty=1";
      }
      if (chk_no_http_status.Checked)
      {
        postBody += "&no_http_status=1";
      }

      if (MessageBox.Show("Are you sure you want to delete the project_id: " + txt_project_id.Text,"Confirm Delete", MessageBoxButtons.YesNo) != DialogResult.Yes)
      {
        return;
      }

      glueResponse tResponse = MakeAPICall("project/v1/delete", "", "POST", postBody);
      if ((tResponse.statusCode == HttpStatusCode.OK) && (!chk_no_http_status.Checked))
      {
        if (tResponse.responseBody != "")
        {
          glue_folder_node[] tTreeNodes = null;
          if (combo_response_format.Text == "json")
          {
            JavaScriptSerializer jSer = new JavaScriptSerializer();
            project_info_response_v1 tJSON = jSer.Deserialize<project_info_response_v1>(tResponse.responseBody);
            if (tJSON.folder_tree != null)
            {
              tTreeNodes = tJSON.folder_tree;
            }
          }
          else if (combo_response_format.Text == "xml")
          {
            XmlSerializer mySerializer = new XmlSerializer(typeof(project_info_response_v1));
            project_info_response_v1 tXML = (project_info_response_v1)mySerializer.Deserialize((new StringReader(tResponse.responseBody)));

            if (tXML.folder_tree != null)
            {
              tTreeNodes = tXML.folder_tree;
            }
          }

          if (tTreeNodes != null)
          {
            RenderTreeNode(tTreeNodes);
          }
        }

        // refresh project list
        butGetProjectList_Click(sender, e);
      }

      ResponseTextBox.Text = tResponse.verboseResponse;
    }

    private void butAddFolder_Click(object sender, EventArgs e)
    {
      string timestamp = GetUNIXEpochTimestamp().ToString();
      string postBody = "";
      postBody += "&company_id=" + HttpUtility.UrlEncode(txt_company_id.Text);
      postBody += "&api_key=" + HttpUtility.UrlEncode(txt_api_key.Text);
      postBody += "&auth_token=" + HttpUtility.UrlEncode(txt_auth_token.Text);
      postBody += "&timestamp=" + HttpUtility.UrlEncode(timestamp);
      postBody += "&sig=" + HttpUtility.UrlEncode(GenerateAPISignature(timestamp));
      postBody += "&sterm=" + HttpUtility.UrlEncode(txt_project_search_term.Text);
      postBody += "&project_id=" + txt_project_id.Text;
      postBody += "&folder_name=" + txt_new_folder.Text;
      postBody += "&parent_folder_id=" + txt_project_object_id.Text;

      if (chk_pretty.Checked)
      {
        postBody += "&pretty=1";
      }
      if (chk_no_http_status.Checked)
      {
        postBody += "&no_http_status=1";
      }

      glueResponse tResponse = MakeAPICall("project/v1/folder/create", "", "POST", postBody);
      if ((tResponse.statusCode == HttpStatusCode.OK) && (!chk_no_http_status.Checked))
      {
        // refresh folder tree
        ShowProjectInfo(txt_project_id.Text);
      }

      ResponseTextBox.Text = tResponse.verboseResponse;
    }

    private void butDeleteFolder_Click(object sender, EventArgs e)
    {
      string timestamp = GetUNIXEpochTimestamp().ToString();
      string postBody = "";
      postBody += "&company_id=" + HttpUtility.UrlEncode(txt_company_id.Text);
      postBody += "&api_key=" + HttpUtility.UrlEncode(txt_api_key.Text);
      postBody += "&auth_token=" + HttpUtility.UrlEncode(txt_auth_token.Text);
      postBody += "&timestamp=" + HttpUtility.UrlEncode(timestamp);
      postBody += "&sig=" + HttpUtility.UrlEncode(GenerateAPISignature(timestamp));
      postBody += "&sterm=" + HttpUtility.UrlEncode(txt_project_search_term.Text);
      postBody += "&project_id=" + txt_project_id.Text;
      postBody += "&folder_id=" + txt_project_object_id.Text;

      if (chk_pretty.Checked)
      {
        postBody += "&pretty=1";
      }
      if (chk_no_http_status.Checked)
      {
        postBody += "&no_http_status=1";
      }

      glueResponse tResponse = MakeAPICall("project/v1/folder/delete", "", "POST", postBody);
      if ((tResponse.statusCode == HttpStatusCode.OK) && (!chk_no_http_status.Checked))
      {
        // refresh folder tree
        ShowProjectInfo(txt_project_id.Text);
      }

      ResponseTextBox.Text = tResponse.verboseResponse;
    }

    private void butRename_Click(object sender, EventArgs e)
    {
      string timestamp = GetUNIXEpochTimestamp().ToString();
      string postBody = "";
      postBody += "&company_id=" + HttpUtility.UrlEncode(txt_company_id.Text);
      postBody += "&api_key=" + HttpUtility.UrlEncode(txt_api_key.Text);
      postBody += "&auth_token=" + HttpUtility.UrlEncode(txt_auth_token.Text);
      postBody += "&timestamp=" + HttpUtility.UrlEncode(timestamp);
      postBody += "&sig=" + HttpUtility.UrlEncode(GenerateAPISignature(timestamp));
      postBody += "&sterm=" + HttpUtility.UrlEncode(txt_project_search_term.Text);
      postBody += "&project_id=" + txt_project_id.Text;
      postBody += "&folder_id=" + txt_project_object_id.Text;
      postBody += "&new_folder_name=" + txt_rename_folder.Text;

      if (chk_pretty.Checked)
      {
        postBody += "&pretty=1";
      }
      if (chk_no_http_status.Checked)
      {
        postBody += "&no_http_status=1";
      }

      glueResponse tResponse = MakeAPICall("project/v1/folder/rename", "", "POST", postBody);
      if ((tResponse.statusCode == HttpStatusCode.OK) && (!chk_no_http_status.Checked))
      {
        // refresh folder tree
        ShowProjectInfo(txt_project_id.Text);
      }

      ResponseTextBox.Text = tResponse.verboseResponse;
    }

    private void butRenameProject_Click(object sender, EventArgs e)
    {
      string timestamp = GetUNIXEpochTimestamp().ToString();
      string postBody = "";
      postBody += "&company_id=" + HttpUtility.UrlEncode(txt_company_id.Text);
      postBody += "&api_key=" + HttpUtility.UrlEncode(txt_api_key.Text);
      postBody += "&auth_token=" + HttpUtility.UrlEncode(txt_auth_token.Text);
      postBody += "&timestamp=" + HttpUtility.UrlEncode(timestamp);
      postBody += "&sig=" + HttpUtility.UrlEncode(GenerateAPISignature(timestamp));
      postBody += "&sterm=" + HttpUtility.UrlEncode(txt_project_search_term.Text);
      postBody += "&project_id=" + txt_project_id.Text;
      postBody += "&new_project_name=" + txt_rename_project.Text;

      if (chk_pretty.Checked)
      {
        postBody += "&pretty=1";
      }
      if (chk_no_http_status.Checked)
      {
        postBody += "&no_http_status=1";
      }

      glueResponse tResponse = MakeAPICall("project/v1/rename", "", "POST", postBody);
      if ((tResponse.statusCode == HttpStatusCode.OK) && (!chk_no_http_status.Checked))
      {
        butGetProjectList_Click(sender, e);
      }

      ResponseTextBox.Text = tResponse.verboseResponse;
    }

    private void butClashReports_Click(object sender, EventArgs e)
    {
      string timestamp = GetUNIXEpochTimestamp().ToString();
      string callArgs = "";
      callArgs += "&company_id=" + HttpUtility.UrlEncode(txt_company_id.Text);
      callArgs += "&api_key=" + HttpUtility.UrlEncode(txt_api_key.Text);
      callArgs += "&auth_token=" + HttpUtility.UrlEncode(txt_auth_token.Text);
      callArgs += "&timestamp=" + HttpUtility.UrlEncode(timestamp);
      callArgs += "&sig=" + HttpUtility.UrlEncode(GenerateAPISignature(timestamp));
      callArgs += "&sterm=" + HttpUtility.UrlEncode(txt_project_search_term.Text);
      callArgs += "&project_id=" + txt_project_id.Text;

      if (chk_pretty.Checked)
      {
        callArgs += "&pretty=1";
      }
      if (chk_no_http_status.Checked)
      {
        callArgs += "&no_http_status=1";
      }

      lvProjectClashReports.Items.Clear();
      glueResponse tResponse = MakeAPICall("project/v1/clashreports", callArgs, "GET", "");
      if ((tResponse.statusCode == HttpStatusCode.OK) && (!chk_no_http_status.Checked))
      {
        if (tResponse.responseBody != "")
        {
          project_clash_reports_response_v1 tServerResponse = null;
          if (combo_response_format.Text == "json")
          {
            JavaScriptSerializer jSer = new JavaScriptSerializer();
            tServerResponse = jSer.Deserialize<project_clash_reports_response_v1>(tResponse.responseBody);
          }
          else if (combo_response_format.Text == "xml")
          {
            XmlSerializer mySerializer = new XmlSerializer(typeof(project_clash_reports_response_v1));
            tServerResponse = (project_clash_reports_response_v1)mySerializer.Deserialize((new StringReader(tResponse.responseBody)));
          }

          model_clash_report[] cReps = tServerResponse.clash_reports;
          if (cReps != null)
          {
            // Show the submodels
            foreach (model_clash_report cRep in cReps)
            {
              AddTwoColumnListViewItem(lvProjectClashReports, cRep.name, cRep.created_by);
            }
          }
          
        }
      }

      ResponseTextBox.Text = tResponse.verboseResponse;
    }

    private void butAddUserToProject_Click(object sender, EventArgs e)
    {
      string timestamp = GetUNIXEpochTimestamp().ToString();
      string postBody = "";
      postBody += "&company_id=" + HttpUtility.UrlEncode(txt_company_id.Text);
      postBody += "&api_key=" + HttpUtility.UrlEncode(txt_api_key.Text);
      postBody += "&auth_token=" + HttpUtility.UrlEncode(txt_auth_token.Text);
      postBody += "&timestamp=" + HttpUtility.UrlEncode(timestamp);
      postBody += "&sig=" + HttpUtility.UrlEncode(GenerateAPISignature(timestamp));
      postBody += "&sterm=" + HttpUtility.UrlEncode(txt_project_search_term.Text);
      postBody += "&project_id=" + txt_project_id.Text;
      postBody += "&login_name=" + HttpUtility.UrlEncode(txt_new_project_user.Text);

      if (chk_pretty.Checked)
      {
        postBody += "&pretty=1";
      }
      if (chk_no_http_status.Checked)
      {
        postBody += "&no_http_status=1";
      }

      glueResponse tResponse = MakeAPICall("project/v1/user/add", "", "POST", postBody);
      if ((tResponse.statusCode == HttpStatusCode.OK) && (!chk_no_http_status.Checked))
      {
        // refresh folder tree
        ShowProjectInfo(txt_project_id.Text);
      }
      else
      {
        ResponseTextBox.Text = tResponse.verboseResponse;
      }
    }

    private void butDeleteUserFromProject_Click(object sender, EventArgs e)
    {
      string timestamp = GetUNIXEpochTimestamp().ToString();
      string postBody = "";
      postBody += "&company_id=" + HttpUtility.UrlEncode(txt_company_id.Text);
      postBody += "&api_key=" + HttpUtility.UrlEncode(txt_api_key.Text);
      postBody += "&auth_token=" + HttpUtility.UrlEncode(txt_auth_token.Text);
      postBody += "&timestamp=" + HttpUtility.UrlEncode(timestamp);
      postBody += "&sig=" + HttpUtility.UrlEncode(GenerateAPISignature(timestamp));
      postBody += "&sterm=" + HttpUtility.UrlEncode(txt_project_search_term.Text);
      postBody += "&project_id=" + txt_project_id.Text;
      postBody += "&login_name=" + HttpUtility.UrlEncode(txt_new_project_user.Text);

      if (chk_pretty.Checked)
      {
        postBody += "&pretty=1";
      }
      if (chk_no_http_status.Checked)
      {
        postBody += "&no_http_status=1";
      }

      glueResponse tResponse = MakeAPICall("project/v1/user/delete", "", "POST", postBody);
      if ((tResponse.statusCode == HttpStatusCode.OK) && (!chk_no_http_status.Checked))
      {
        // refresh folder tree
        ShowProjectInfo(txt_project_id.Text);
      }

      ResponseTextBox.Text = tResponse.verboseResponse;
    }

    private void butGetNotifications_Click(object sender, EventArgs e)
    {
      string timestamp = GetUNIXEpochTimestamp().ToString();
      string callArgs = "";
      callArgs += "&company_id=" + HttpUtility.UrlEncode(txt_company_id.Text);
      callArgs += "&api_key=" + HttpUtility.UrlEncode(txt_api_key.Text);
      callArgs += "&auth_token=" + HttpUtility.UrlEncode(txt_auth_token.Text);
      callArgs += "&timestamp=" + HttpUtility.UrlEncode(timestamp);
      callArgs += "&sig=" + HttpUtility.UrlEncode(GenerateAPISignature(timestamp));
      callArgs += "&sterm=" + HttpUtility.UrlEncode(txt_project_search_term.Text);
      callArgs += "&project_id=" + txt_project_id.Text;
      callArgs += "&proxy_login_name=" + HttpUtility.UrlEncode(txt_new_project_user.Text);

      if (chk_pretty.Checked)
      {
        callArgs += "&pretty=1";
      }
      if (chk_no_http_status.Checked)
      {
        callArgs += "&no_http_status=1";
      }

      glueResponse tResponse = MakeAPICall("project/v1/user/notification/get_settings", callArgs, "GET", "");
      if ((tResponse.statusCode == HttpStatusCode.OK) && (!chk_no_http_status.Checked))
      {
        // refresh folder tree
        ShowProjectInfo(txt_project_id.Text);
      }

      ResponseTextBox.Text = tResponse.verboseResponse;
    }

    private void butSetUserNotifications_Click(object sender, EventArgs e)
    {
      string timestamp = GetUNIXEpochTimestamp().ToString();
      string postBody = "";
      postBody += "&company_id=" + HttpUtility.UrlEncode(txt_company_id.Text);
      postBody += "&api_key=" + HttpUtility.UrlEncode(txt_api_key.Text);
      postBody += "&auth_token=" + HttpUtility.UrlEncode(txt_auth_token.Text);
      postBody += "&timestamp=" + HttpUtility.UrlEncode(timestamp);
      postBody += "&sig=" + HttpUtility.UrlEncode(GenerateAPISignature(timestamp));
      postBody += "&sterm=" + HttpUtility.UrlEncode(txt_project_search_term.Text);
      postBody += "&project_id=" + txt_project_id.Text;
      postBody += "&proxy_login_name=" + HttpUtility.UrlEncode(txt_new_project_user.Text);

      // Get a UI on this eventually
      postBody += "&nsettings=" + HttpUtility.UrlEncode("model;view;budget;rfi;submittal");

      if (chk_pretty.Checked)
      {
        postBody += "&pretty=1";
      }
      if (chk_no_http_status.Checked)
      {
        postBody += "&no_http_status=1";
      }

      glueResponse tResponse = MakeAPICall("project/v1/user/notification/set_settings", "", "POST", postBody);
      if ((tResponse.statusCode == HttpStatusCode.OK) && (!chk_no_http_status.Checked))
      {
        // refresh folder tree
        ShowProjectInfo(txt_project_id.Text);
      }

      ResponseTextBox.Text = tResponse.verboseResponse;
    }

    private void butNotificationList_Click(object sender, EventArgs e)
    {
      string timestamp = GetUNIXEpochTimestamp().ToString();
      string callArgs = "";
      callArgs += "&company_id=" + HttpUtility.UrlEncode(txt_company_id.Text);
      callArgs += "&api_key=" + HttpUtility.UrlEncode(txt_api_key.Text);
      callArgs += "&auth_token=" + HttpUtility.UrlEncode(txt_auth_token.Text);
      callArgs += "&timestamp=" + HttpUtility.UrlEncode(timestamp);
      callArgs += "&sig=" + HttpUtility.UrlEncode(GenerateAPISignature(timestamp));
      callArgs += "&project_id=" + HttpUtility.UrlEncode(txt_project_id.Text);

      if (chk_service_provider.Checked)
      {
        if (txt_new_project_user.Text != "")
        {
          callArgs += "&login_name=" + HttpUtility.UrlEncode(txt_new_project_user.Text);
        }
      }

      callArgs += "&page=" + num_notification_page.Value.ToString();
      callArgs += "&page_size=" + num_notification_page_size.Value.ToString();

      if (chk_pretty.Checked)
      {
        callArgs += "&pretty=1";
      }
      if (chk_no_http_status.Checked)
      {
        callArgs += "&no_http_status=1";
      }

      // lvActionList.Items.Clear();
      glueResponse tResponse = MakeAPICall("project/v1/user/notification/list", callArgs, "GET", "");
      if ((tResponse.statusCode == HttpStatusCode.OK) && (!chk_no_http_status.Checked))
      {
        if (tResponse.responseBody != "")
        {
        }
      }

      ResponseTextBox.Text = tResponse.verboseResponse;
    }

    // Handle the After_Select event.
    private void TreeView_AfterSelect(System.Object sender, System.Windows.Forms.TreeViewEventArgs e)
    {
      string[] tParts = e.Node.Tag.ToString().Split(';');

      rnListView.Items.Clear();

      // Nodes: node.Tag = "FOLDER;" + nodeInfo.object_id + ";" + nodeInfo.version_id + ";" + nodeInfo.version + ";" + nodeInfo.name;
      if (tParts[0] == "FOLDER")
      {
        txt_project_object_id.Text = tParts[1];
        txt_selected_folder_name.Text = HttpUtility.UrlDecode(tParts[4]);
        txt_upload_folder_name.Text = HttpUtility.UrlDecode(tParts[4]);

        txt_equip_model_id.Text = "";
        txt_equip_model_version_id.Text = "";

      }
      else
      {     if (tParts.Length >= 3)
                {
                    txt_equip_model_id.Text = tParts[1];
                    txt_equip_model_version_id.Text = tParts[2];
                }
            }

    }

    private void RNListView_ItemClick(Object sender, ListViewItemSelectionChangedEventArgs e)
    {
       
    }

    #endregion Project Service Group

    #region Action Service Group

    private void butViewInfo_Click(object sender, EventArgs e)
    {
      string timestamp = GetUNIXEpochTimestamp().ToString();
      string callArgs = "";
      callArgs += "&company_id=" + HttpUtility.UrlEncode(txt_company_id.Text);
      callArgs += "&api_key=" + HttpUtility.UrlEncode(txt_api_key.Text);
      callArgs += "&auth_token=" + HttpUtility.UrlEncode(txt_auth_token.Text);
      callArgs += "&timestamp=" + HttpUtility.UrlEncode(timestamp);
      callArgs += "&sig=" + HttpUtility.UrlEncode(GenerateAPISignature(timestamp));
      callArgs += "&view_id=" + HttpUtility.UrlEncode(txt_view_id.Text);

      if (chk_pretty.Checked)
      {
        callArgs += "&pretty=1";
      }
      if (chk_no_http_status.Checked)
      {
        callArgs += "&no_http_status=1";
      }

      lvActionList.Items.Clear();
      glueResponse tResponse = MakeAPICall("model/v1/view_info", callArgs, "GET", "");
      if ((tResponse.statusCode == HttpStatusCode.OK) && (!chk_no_http_status.Checked))
      {
        if (tResponse.responseBody != "")
        {
          if (combo_response_format.Text == "json")
          {
            JavaScriptSerializer jSer = new JavaScriptSerializer();
            model_view_node tJSON = jSer.Deserialize<model_view_node>(tResponse.responseBody);
          }
          else if (combo_response_format.Text == "xml")
          {
            XmlSerializer mySerializer = new XmlSerializer(typeof(project_info_response_v1));
            model_view_node tXML = (model_view_node)mySerializer.Deserialize((new StringReader(tResponse.responseBody)));
          }
        }
      }

      ResponseTextBox.Text = tResponse.verboseResponse;
    }

    private void butGetSingleActionID_Click(object sender, EventArgs e)
    {
      string timestamp = GetUNIXEpochTimestamp().ToString();
      string callArgs = "";
      callArgs += "&company_id=" + HttpUtility.UrlEncode(txt_company_id.Text);
      callArgs += "&api_key=" + HttpUtility.UrlEncode(txt_api_key.Text);
      callArgs += "&auth_token=" + HttpUtility.UrlEncode(txt_auth_token.Text);
      callArgs += "&timestamp=" + HttpUtility.UrlEncode(timestamp);
      callArgs += "&sig=" + HttpUtility.UrlEncode(GenerateAPISignature(timestamp));
      callArgs += "&action_id=" + HttpUtility.UrlEncode(txt_action_search_action_id.Text);

      if (chk_pretty.Checked)
      {
        callArgs += "&pretty=1";
      }
      if (chk_no_http_status.Checked)
      {
        callArgs += "&no_http_status=1";
      }

      lvActionList.Items.Clear();
      glueResponse tResponse = MakeAPICall("action/v1/info", callArgs, "GET", "");
      if ((tResponse.statusCode == HttpStatusCode.OK) && (!chk_no_http_status.Checked))
      {
        if (tResponse.responseBody != "")
        {
          if (combo_response_format.Text == "json")
          {
            JavaScriptSerializer jSer = new JavaScriptSerializer();
            action_info tJSON = jSer.Deserialize<action_info>(tResponse.responseBody);
          }
          else if (combo_response_format.Text == "xml")
          {
            XmlSerializer mySerializer = new XmlSerializer(typeof(project_info_response_v1));
            action_info tXML = (action_info)mySerializer.Deserialize((new StringReader(tResponse.responseBody)));
          }
        }
      }

      ResponseTextBox.Text = tResponse.verboseResponse;
    }

    private void ActionProjectListView_ItemClick(Object sender, ListViewItemSelectionChangedEventArgs e)
    {
      if (e.IsSelected)
      {
        // Clear the action list...
        lvActionList.Items.Clear();
        txt_action_project_id.Text = this.lvActionProjects.Items[e.ItemIndex].SubItems[2].Text;

        ListView aListView = lvModels;
        string projectID = txt_action_project_id.Text;
        string timestamp = GetUNIXEpochTimestamp().ToString();
        string callArgs = "";
        callArgs += "&company_id=" + HttpUtility.UrlEncode(txt_company_id.Text);
        callArgs += "&api_key=" + HttpUtility.UrlEncode(txt_api_key.Text);
        callArgs += "&auth_token=" + HttpUtility.UrlEncode(txt_auth_token.Text);
        callArgs += "&timestamp=" + HttpUtility.UrlEncode(timestamp);
        callArgs += "&sig=" + HttpUtility.UrlEncode(GenerateAPISignature(timestamp));
        callArgs += "&sterm=" + HttpUtility.UrlEncode(txt_project_search_term.Text);
        callArgs += "&project_id=" + projectID;

        if (chk_lightweight.Checked)
        {
          callArgs += "&lightweight=1";
        }

        if (chk_pretty.Checked)
        {
          callArgs += "&pretty=1";
        }
        if (chk_no_http_status.Checked)
        {
          callArgs += "&no_http_status=1";
        }

        glueResponse tResponse = MakeAPICall("project/v1/info", callArgs, "GET", "");
        if ((tResponse.statusCode == HttpStatusCode.OK) && (!chk_no_http_status.Checked))
        {
          if (tResponse.responseBody != "")
          {
            glue_folder_node[] tTreeNodes = null;
            if (combo_response_format.Text == "json")
            {
              JavaScriptSerializer jSer = new JavaScriptSerializer();
              project_info_response_v1 tJSON = jSer.Deserialize<project_info_response_v1>(tResponse.responseBody);
              if (tJSON.folder_tree != null)
              {
                tTreeNodes = tJSON.folder_tree;
              }
            }
            else if (combo_response_format.Text == "xml")
            {
              XmlSerializer mySerializer = new XmlSerializer(typeof(project_info_response_v1));
              project_info_response_v1 tXML = (project_info_response_v1)mySerializer.Deserialize((new StringReader(tResponse.responseBody)));

              if (tXML.folder_tree != null)
              {
                tTreeNodes = tXML.folder_tree;
              }
            }

            aListView.Items.Clear();
            if (tTreeNodes != null)
            {
              foreach (glue_folder_node nodeInfo in tTreeNodes)
              {
                if (nodeInfo.type == "FOLDER")
                {
                  if (nodeInfo.folder_contents != null)
                  {
                    RenderTreeNodeModelsOnly(aListView, nodeInfo.folder_contents);
                  }
                }
                else if (nodeInfo.type == "MODEL")
                {
                  ListViewItem newItem = new ListViewItem(HttpUtility.UrlDecode(nodeInfo.name)); //Parent item
                  ListViewItem.ListViewSubItem aSubItem1 = new ListViewItem.ListViewSubItem(newItem, nodeInfo.object_id); //Creating subitems for the parent item
                  newItem.SubItems.Add(aSubItem1); //Associating these subitems to the parent item
                  aListView.Items.Add(newItem); //Adding the parent item to the listview control
                }
              }
            }
          }
        }

        ResponseTextBox.Text = "Models for the Project Loaded";
      }
    }

    void RenderTreeNodeModelsOnly(ListView aListView, glue_folder_node[] tTreeNodes)
    {
      foreach (glue_folder_node nodeInfo in tTreeNodes)
      {
        if (nodeInfo.type == "FOLDER")
        {
          if (nodeInfo.folder_contents != null)
          {
            RenderTreeNodeModelsOnly(aListView, nodeInfo.folder_contents);
          }
        }
        else if (nodeInfo.type == "MODEL")
        {
          ListViewItem newItem = new ListViewItem(HttpUtility.UrlDecode(nodeInfo.name)); //Parent item
          ListViewItem.ListViewSubItem aSubItem1 = new ListViewItem.ListViewSubItem(newItem, nodeInfo.object_id); //Creating subitems for the parent item
          newItem.SubItems.Add(aSubItem1); //Associating these subitems to the parent item
          aListView.Items.Add(newItem); //Adding the parent item to the listview control
        }
      }
    }

    private void butActionProjectList_Click(object sender, EventArgs e)
    {
      lvModels.Items.Clear();
      lvActionList.Items.Clear();

      butClearGUI_Click(sender, e);
      ShowProjectList(lvActionProjects, txt_action_project_search_term.Text, Convert.ToInt32(num_action_project_page.Value), Convert.ToInt32(num_action_project_page_size.Value));
    }

    void RenderActions(ListView aListview, action_info[] tActions)
    {
      aListview.Items.Clear();
      foreach (action_info actionInfo in tActions)
      {
        ListViewItem newItem = new ListViewItem(HttpUtility.UrlDecode(actionInfo.action_id)); //Parent item
        ListViewItem.ListViewSubItem aSubItem1 = new ListViewItem.ListViewSubItem(newItem, HttpUtility.UrlDecode(actionInfo.type)); //Creating subitems for the parent item
        ListViewItem.ListViewSubItem aSubItem2 = new ListViewItem.ListViewSubItem(newItem, HttpUtility.UrlDecode(actionInfo.type_object_id));
        ListViewItem.ListViewSubItem aSubItem3 = new ListViewItem.ListViewSubItem(newItem, HttpUtility.UrlDecode(actionInfo.subject));
        ListViewItem.ListViewSubItem aSubItem4 = new ListViewItem.ListViewSubItem(newItem, HttpUtility.UrlDecode(actionInfo.model_name));
        ListViewItem.ListViewSubItem aSubItem5 = new ListViewItem.ListViewSubItem(newItem, actionInfo.model_id);
        ListViewItem.ListViewSubItem aSubItem6 = new ListViewItem.ListViewSubItem(newItem, actionInfo.created_by);
        ListViewItem.ListViewSubItem aSubItem7 = new ListViewItem.ListViewSubItem(newItem, actionInfo.created_date);
        newItem.SubItems.Add(aSubItem1); //Associating these subitems to the parent item
        newItem.SubItems.Add(aSubItem2);
        newItem.SubItems.Add(aSubItem3);
        newItem.SubItems.Add(aSubItem4);
        newItem.SubItems.Add(aSubItem5);
        newItem.SubItems.Add(aSubItem6);
        newItem.SubItems.Add(aSubItem7);
        aListview.Items.Add(newItem); //Adding the parent item to the listview control
      }
    }

    private void butGetActions_Click(object sender, EventArgs e)
    {
      string timestamp = GetUNIXEpochTimestamp().ToString();
      string callArgs = "";
      callArgs += "&company_id=" + HttpUtility.UrlEncode(txt_company_id.Text);
      callArgs += "&api_key=" + HttpUtility.UrlEncode(txt_api_key.Text);
      callArgs += "&auth_token=" + HttpUtility.UrlEncode(txt_auth_token.Text);
      callArgs += "&timestamp=" + HttpUtility.UrlEncode(timestamp);
      callArgs += "&sig=" + HttpUtility.UrlEncode(GenerateAPISignature(timestamp));
      callArgs += "&sterm=" + HttpUtility.UrlEncode(txt_project_search_term.Text);
      callArgs += "&project_id=" + HttpUtility.UrlEncode(txt_action_project_id.Text);

      // Get the model 
      string modInfo = "";
      foreach (ListViewItem selectedItem in lvModels.SelectedItems)
      {
        modInfo = "&model_id=" + selectedItem.SubItems[1].Text;
      }
      callArgs += modInfo;
      if (txt_action_login_name.Text != "")
      {
        //callArgs += "&user_id=" + 
        callArgs += "&login_name=" + HttpUtility.UrlEncode(txt_action_login_name.Text);
      }

      // Setup the Action Types
      string tTypes = "";
      string tCon = "";
      foreach (object selectedItem in lbActionTypes.SelectedItems)
      {
        tTypes += tCon + selectedItem.ToString();
        tCon = ";";
      }
      if (tTypes != "")
      {
        callArgs += "&type=" + tTypes;
      }

      callArgs += "&page=" + num_action_page.Value.ToString();
      callArgs += "&page_size=" + num_action_page_size.Value.ToString();

      if (chk_pretty.Checked)
      {
        callArgs += "&pretty=1";
      }
      if (chk_no_http_status.Checked)
      {
        callArgs += "&no_http_status=1";
      }

      lvActionList.Items.Clear();
      glueResponse tResponse = MakeAPICall("action/v1/search", callArgs, "GET", "");
      if ((tResponse.statusCode == HttpStatusCode.OK) && (!chk_no_http_status.Checked))
      {
        if (tResponse.responseBody != "")
        {
          action_info[] tActions = null;
          if (combo_response_format.Text == "json")
          {
            JavaScriptSerializer jSer = new JavaScriptSerializer();
            action_search_response_v1 tJSON = jSer.Deserialize<action_search_response_v1>(tResponse.responseBody);
            if (tJSON.action_list != null)
            {
              tActions = tJSON.action_list;
            }
          }
          else if (combo_response_format.Text == "xml")
          {
            XmlSerializer mySerializer = new XmlSerializer(typeof(project_info_response_v1));
            action_search_response_v1 tXML = (action_search_response_v1)mySerializer.Deserialize((new StringReader(tResponse.responseBody)));

            if (tXML.action_list != null)
            {
              tActions = tXML.action_list;
            }
          }

          if (tActions != null)
          {
            RenderActions(lvActionList, tActions);
          }
        }
      }

      ResponseTextBox.Text = tResponse.verboseResponse;
    }

    private void butClearGUI_Click(object sender, EventArgs e)
    {
      lvModels.SelectedItems.Clear();
      lbActionTypes.SelectedItems.Clear();
      txt_action_login_name.Text = "";
      num_action_page.Value = 1;
      num_action_page_size.Value = 20;
      lvActionList.Items.Clear();
    }

    private void ActionSearch_ItemClick(Object sender, ListViewItemSelectionChangedEventArgs e)
    {
      if (e.IsSelected)
      {
        txt_action_search_action_id.Text = this.lvActionList.Items[e.ItemIndex].SubItems[0].Text;
        if (this.lvActionList.Items[e.ItemIndex].SubItems[1].Text == "view")
        {
          txt_view_id.Text = this.lvActionList.Items[e.ItemIndex].SubItems[2].Text;
        }
        else
        {
          txt_view_id.Text = "";
        }
      }
    }

    private void butViewModel_Click(object sender, EventArgs e)
    {
      if (txt_action_search_action_id.Text == "")
      {
        MessageBox.Show("Please select an action with a valid Action ID.", "Action ID Required", MessageBoxButtons.OK, MessageBoxIcon.Information);
        return;
      }

      ShowModel(txt_action_search_action_id.Text);
    }

    #endregion Action Service Group

    #region Model Group

    private void butViewTreeInfo_Click(object sender, EventArgs e)
    {
      string timestamp = GetUNIXEpochTimestamp().ToString();
      string callArgs = "";
      callArgs += "&company_id=" + HttpUtility.UrlEncode(txt_company_id.Text);
      callArgs += "&api_key=" + HttpUtility.UrlEncode(txt_api_key.Text);
      callArgs += "&auth_token=" + HttpUtility.UrlEncode(txt_auth_token.Text);
      callArgs += "&timestamp=" + HttpUtility.UrlEncode(timestamp);
      callArgs += "&sig=" + HttpUtility.UrlEncode(GenerateAPISignature(timestamp));
      callArgs += "&sterm=" + HttpUtility.UrlEncode(txt_project_search_term.Text);

      // Get the model 
      foreach (ListViewItem selectedItem in lvModelModels.SelectedItems)
      {
        callArgs += "&model_id=" + selectedItem.SubItems[2].Text;
        // callArgs += "&version_id=" + selectedItem.SubItems[3].Text;       
      }

      if (chk_pretty.Checked)
      {
        callArgs += "&pretty=1";
      }
      if (chk_no_http_status.Checked)
      {
        callArgs += "&no_http_status=1";
      }

      lvModelInfo.Items.Clear();
      lvSubModels.Items.Clear();
      lvClashReps.Items.Clear();
      lvViews.Items.Clear();
      lvMarkups.Items.Clear();
      glueResponse tResponse = MakeAPICall("model/v1/view_tree", callArgs, "GET", "");
      if ((tResponse.statusCode == HttpStatusCode.OK) && (!chk_no_http_status.Checked))
      {
        if (tResponse.responseBody != "")
        {
          model_view_tree_response_v1 tServerResponse = null;
          if (combo_response_format.Text == "json")
          {
            JavaScriptSerializer jSer = new JavaScriptSerializer();
            tServerResponse = jSer.Deserialize<model_view_tree_response_v1>(tResponse.responseBody);
          }
          else if (combo_response_format.Text == "xml")
          {
            XmlSerializer mySerializer = new XmlSerializer(typeof(model_view_tree_response_v1));
            tServerResponse = (model_view_tree_response_v1)mySerializer.Deserialize((new StringReader(tResponse.responseBody)));
          }
        }
      }

      ResponseTextBox.Text = tResponse.verboseResponse;
    }

    public void ShowThumbnail(string aSize)
    {
      if (gViewer == null)
      {
        gViewer = new BIM360DisplayComponent();
      }
      // Build the URL to view the model
      string timestamp = GetUNIXEpochTimestamp().ToString();
      string tURL = "";
      tURL += this.txt_base_url.Text;
      tURL += "/model/v1/thumbnail?";
      tURL += "company_id=" + txt_company_id.Text;
      tURL += "&api_key=" + txt_api_key.Text;
      tURL += "&auth_token=" + txt_auth_token.Text;
      tURL += "&timestamp=" + timestamp;
      tURL += "&sig=" + HttpUtility.UrlEncode(GenerateAPISignature(timestamp));
      tURL += "&model_id=" + txt_model_model_id.Text;
      tURL += "&size=" + aSize;

      gViewer.isDownloadOperation = false;
      gViewer.Show();
      gViewer.webBrowser.Url = new System.Uri(tURL);
    }

    private void butModelThumb175_Click(object sender, EventArgs e)
    {
      ShowThumbnail("175x175");
    }

    private void butModelThumb300_Click(object sender, EventArgs e)
    {
      ShowThumbnail("300x300");
    }

    private void butModelProjectList_Click(object sender, EventArgs e)
    {
      this.lvModelModels.Items.Clear();
      ShowProjectList(lvModelProjects, txt_model_search_term.Text, Convert.ToInt32(this.num_model_page.Value), Convert.ToInt32(this.num_model_page_size.Value));
    }

    private void ModelProjectListView_ItemClick(Object sender, ListViewItemSelectionChangedEventArgs e)
    {
      if (e.IsSelected)
      {
        txt_model_project_id.Text = this.lvModelProjects.Items[e.ItemIndex].SubItems[2].Text;

        ListView aListView = lvModelModels;
        string projectID = txt_model_project_id.Text;
        string timestamp = GetUNIXEpochTimestamp().ToString();
        string callArgs = "";
        callArgs += "&company_id=" + HttpUtility.UrlEncode(txt_company_id.Text);
        callArgs += "&api_key=" + HttpUtility.UrlEncode(txt_api_key.Text);
        callArgs += "&auth_token=" + HttpUtility.UrlEncode(txt_auth_token.Text);
        callArgs += "&timestamp=" + HttpUtility.UrlEncode(timestamp);
        callArgs += "&sig=" + HttpUtility.UrlEncode(GenerateAPISignature(timestamp));
        callArgs += "&sterm=" + HttpUtility.UrlEncode(txt_project_search_term.Text);
        callArgs += "&project_id=" + projectID;

        if (chk_pretty.Checked)
        {
          callArgs += "&pretty=1";
        }
        if (chk_no_http_status.Checked)
        {
          callArgs += "&no_http_status=1";
        }

        glueResponse tResponse = MakeAPICall("project/v1/info", callArgs, "GET", "");
        if ((tResponse.statusCode == HttpStatusCode.OK) && (!chk_no_http_status.Checked))
        {
          if (tResponse.responseBody != "")
          {
            glue_folder_node[] tTreeNodes = null;
            if (combo_response_format.Text == "json")
            {
              JavaScriptSerializer jSer = new JavaScriptSerializer();
              project_info_response_v1 tJSON = jSer.Deserialize<project_info_response_v1>(tResponse.responseBody);
              if (tJSON.folder_tree != null)
              {
                tTreeNodes = tJSON.folder_tree;
              }
            }
            else if (combo_response_format.Text == "xml")
            {
              XmlSerializer mySerializer = new XmlSerializer(typeof(project_info_response_v1));
              project_info_response_v1 tXML = (project_info_response_v1)mySerializer.Deserialize((new StringReader(tResponse.responseBody)));

              if (tXML.folder_tree != null)
              {
                tTreeNodes = tXML.folder_tree;
              }
            }

            aListView.Items.Clear();
            if (tTreeNodes != null)
            {
              LoadModelList(aListView, tTreeNodes);
            }
          }
        }

        ResponseTextBox.Text = "Models for the Project Loaded";
      }
    }

    void LoadModelList(ListView aListView, glue_folder_node[] tTreeNodes)
    {
      foreach (glue_folder_node nodeInfo in tTreeNodes)
      {
        if (nodeInfo.type == "FOLDER")
        {
          if (nodeInfo.folder_contents != null)
          {
            LoadModelList(aListView, nodeInfo.folder_contents);
          }
        }
        else if (nodeInfo.type == "MODEL")
        {
          ListViewItem newItem = new ListViewItem(HttpUtility.UrlDecode(nodeInfo.name)); //Parent item
          ListViewItem.ListViewSubItem aSubItem1 = new ListViewItem.ListViewSubItem(newItem, nodeInfo.version.ToString()); //Creating subitems for the parent item
          ListViewItem.ListViewSubItem aSubItem2 = new ListViewItem.ListViewSubItem(newItem, nodeInfo.object_id); //Creating subitems for the parent item
          ListViewItem.ListViewSubItem aSubItem3 = new ListViewItem.ListViewSubItem(newItem, nodeInfo.version_id); //Creating subitems for the parent item
          newItem.SubItems.Add(aSubItem1); //Associating these subitems to the parent item
          newItem.SubItems.Add(aSubItem2); //Associating these subitems to the parent item
          newItem.SubItems.Add(aSubItem3); //Associating these subitems to the parent item
          aListView.Items.Add(newItem); //Adding the parent item to the listview control
        }
      }
    }

    private void butModelInfo_Click(object sender, EventArgs e)
    {
      string timestamp = GetUNIXEpochTimestamp().ToString();
      string callArgs = "";
      callArgs += "&company_id=" + HttpUtility.UrlEncode(txt_company_id.Text);
      callArgs += "&api_key=" + HttpUtility.UrlEncode(txt_api_key.Text);
      callArgs += "&auth_token=" + HttpUtility.UrlEncode(txt_auth_token.Text);
      callArgs += "&timestamp=" + HttpUtility.UrlEncode(timestamp);
      callArgs += "&sig=" + HttpUtility.UrlEncode(GenerateAPISignature(timestamp));
      callArgs += "&sterm=" + HttpUtility.UrlEncode(txt_project_search_term.Text);

      // Get the model 
      foreach (ListViewItem selectedItem in lvModelModels.SelectedItems)
      {
        callArgs += "&model_id=" + selectedItem.SubItems[2].Text;
        // callArgs += "&version_id=" + selectedItem.SubItems[3].Text;       
      }

      if (chk_pretty.Checked)
      {
        callArgs += "&pretty=1";
      }
      if (chk_no_http_status.Checked)
      {
        callArgs += "&no_http_status=1";
      }

      lvModelInfo.Items.Clear();
      lvSubModels.Items.Clear();
      lvClashReps.Items.Clear();
      lvViews.Items.Clear();
      lvMarkups.Items.Clear();
      glueResponse tResponse = MakeAPICall("model/v1/info", callArgs, "GET", "");
      if ((tResponse.statusCode == HttpStatusCode.OK) && (!chk_no_http_status.Checked))
      {
        if (tResponse.responseBody != "")
        {
          model_info_response_v1 tServerResponse = null;
          if (combo_response_format.Text == "json")
          {
            JavaScriptSerializer jSer = new JavaScriptSerializer();
            tServerResponse = jSer.Deserialize<model_info_response_v1>(tResponse.responseBody);
          }
          else if (combo_response_format.Text == "xml")
          {
            XmlSerializer mySerializer = new XmlSerializer(typeof(model_info_response_v1));
            tServerResponse = (model_info_response_v1)mySerializer.Deserialize((new StringReader(tResponse.responseBody)));
          }

          AddTwoColumnListViewItem(lvModelInfo, "company_id", tServerResponse.company_id);
          AddTwoColumnListViewItem(lvModelInfo, "project_id", tServerResponse.project_id);
          AddTwoColumnListViewItem(lvModelInfo, "model_name", tServerResponse.model_name);
          AddTwoColumnListViewItem(lvModelInfo, "model_id", tServerResponse.model_id);
          AddTwoColumnListViewItem(lvModelInfo, "model_version", tServerResponse.model_version.ToString());
          AddTwoColumnListViewItem(lvModelInfo, "model_version_id", tServerResponse.model_version_id);
          AddTwoColumnListViewItem(lvModelInfo, "is_merged_model", tServerResponse.is_merged_model.ToString());
          AddTwoColumnListViewItem(lvModelInfo, "action_id", tServerResponse.action_id);
          AddTwoColumnListViewItem(lvModelInfo, "created_by", tServerResponse.created_by);
          AddTwoColumnListViewItem(lvModelInfo, "created_date", tServerResponse.created_date);
          AddTwoColumnListViewItem(lvModelInfo, "modified_by", tServerResponse.modified_by);
          AddTwoColumnListViewItem(lvModelInfo, "modified_date", tServerResponse.modified_date);
          AddTwoColumnListViewItem(lvModelInfo, "parent_folder_id", tServerResponse.parent_folder_id);
          AddTwoColumnListViewItem(lvModelInfo, "file_parsed_status", tServerResponse.file_parsed_status.ToString());

          // Set Action ID
          txt_model_action_id.Text = tServerResponse.action_id;

          glue_folder_node[] subModels = tServerResponse.merged_submodels;
          if (subModels != null)
          {
            // Show the submodels
            foreach (glue_folder_node aSubModel in subModels)
            {
              AddTwoColumnListViewItem(lvSubModels, aSubModel.name, aSubModel.version.ToString());
            }
          }

          model_clash_report[] cReps = tServerResponse.clash_reports;
          if (cReps != null)
          {
            // Show the submodels
            foreach (model_clash_report cRep in cReps)
            {
                AddTwoColumnListViewItem(lvClashReps, HttpUtility.UrlDecode(cRep.name), cRep.created_by);
            }
          }

          txt_model_download_url.Text = buildDownloadURL(tServerResponse.model_id, txt_model_project_id.Text);
          txt_model_model_id.Text = tServerResponse.model_id;

          RenderViewNode(lvViews, tServerResponse.view_tree);
        }
      }

      ResponseTextBox.Text = tResponse.verboseResponse;
    }

    private string buildDownloadURL(string aModelID, string aProjectID, Boolean noBaseURL = false)
    {
      string callArgs = "";

      // Set the download URL string
      string timestamp = GetUNIXEpochTimestamp().ToString();
      callArgs = "";
      callArgs += "company_id=" + HttpUtility.UrlEncode(txt_company_id.Text);
      callArgs += "&api_key=" + HttpUtility.UrlEncode(txt_api_key.Text);
      callArgs += "&auth_token=" + HttpUtility.UrlEncode(txt_auth_token.Text);
      callArgs += "&timestamp=" + HttpUtility.UrlEncode(timestamp);
      callArgs += "&sig=" + HttpUtility.UrlEncode(GenerateAPISignature(timestamp));
      callArgs += "&project_id=" + HttpUtility.UrlEncode(aProjectID);
      callArgs += "&model_id=" + aModelID;

      string url = "";
      if (!noBaseURL)
      {
        url = txt_base_url.Text;
        if (txt_base_url.Text.Substring(txt_base_url.Text.Length - 1) != "/")
        {
          url += "/";
        }
      }
      url += "model/v1/download?" + callArgs;
      return url;
    }

    private void AddTwoColumnListViewItem(ListView aListView, string key, string value)
    {
      ListViewItem newItem = new ListViewItem(key);
      newItem.SubItems.Add(new ListViewItem.ListViewSubItem(newItem, value));
      aListView.Items.Add(newItem);
    }

    void RenderViewNode(ListView aListview, model_view_node[] tViewNodes)
    {
      // On null...just return
      if (tViewNodes == null)
      {
        return;
      }

      foreach (model_view_node nodeInfo in tViewNodes)
      {
        ListViewItem newItem = new ListViewItem(HttpUtility.UrlDecode(nodeInfo.type));
        newItem.SubItems.Add(new ListViewItem.ListViewSubItem(newItem, HttpUtility.UrlDecode(nodeInfo.name)));
        newItem.SubItems.Add(new ListViewItem.ListViewSubItem(newItem, nodeInfo.created_date));
        newItem.SubItems.Add(new ListViewItem.ListViewSubItem(newItem, nodeInfo.created_by));
        newItem.SubItems.Add(new ListViewItem.ListViewSubItem(newItem, nodeInfo.action_id));
        aListview.Items.Add(newItem);
        if (nodeInfo.type == "FOLDER")
        {
          if (nodeInfo.folder_contents != null)
          {
            RenderViewNode(aListview, nodeInfo.folder_contents);
          }
        }
        else if (nodeInfo.type == "VIEW")
        {
          if (nodeInfo.markups != null)
          {
            foreach (model_markup markupInfo in nodeInfo.markups)
            {
              ListViewItem markupNewItem = new ListViewItem("MARKUP");
              markupNewItem.SubItems.Add(new ListViewItem.ListViewSubItem(markupNewItem, markupInfo.name));
              markupNewItem.SubItems.Add(new ListViewItem.ListViewSubItem(markupNewItem, nodeInfo.created_date));
              markupNewItem.SubItems.Add(new ListViewItem.ListViewSubItem(markupNewItem, nodeInfo.created_by));
              markupNewItem.SubItems.Add(new ListViewItem.ListViewSubItem(newItem, nodeInfo.action_id));
              aListview.Items.Add(markupNewItem);
            }
          }
        }
      }
    }

    private void ModelModelsListView_ItemClick(Object sender, ListViewItemSelectionChangedEventArgs e)
    {
      if (e.IsSelected)
      {
        butModelInfo_Click(sender, e);
      }
    }

    private void butGetAllMarkups_Click(object sender, EventArgs e)
    {
      string timestamp = GetUNIXEpochTimestamp().ToString();
      string callArgs = "";
      callArgs += "&company_id=" + HttpUtility.UrlEncode(txt_company_id.Text);
      callArgs += "&api_key=" + HttpUtility.UrlEncode(txt_api_key.Text);
      callArgs += "&auth_token=" + HttpUtility.UrlEncode(txt_auth_token.Text);
      callArgs += "&timestamp=" + HttpUtility.UrlEncode(timestamp);
      callArgs += "&sig=" + HttpUtility.UrlEncode(GenerateAPISignature(timestamp));

      // Get the model 
      foreach (ListViewItem selectedItem in lvModelModels.SelectedItems)
      {
        callArgs += "&model_id=" + selectedItem.SubItems[2].Text;
      }

      if (chk_pretty.Checked)
      {
        callArgs += "&pretty=1";
      }
      if (chk_no_http_status.Checked)
      {
        callArgs += "&no_http_status=1";
      }

      lvMarkups.Items.Clear();
      glueResponse tResponse = MakeAPICall("model/v1/markups", callArgs, "GET", "");
      if ((tResponse.statusCode == HttpStatusCode.OK) && (!chk_no_http_status.Checked))
      {
        if (tResponse.responseBody != "")
        {
          model_markups_response_v1 tServerResponse = null;
          if (combo_response_format.Text == "json")
          {
            JavaScriptSerializer jSer = new JavaScriptSerializer();
            tServerResponse = jSer.Deserialize<model_markups_response_v1>(tResponse.responseBody);
          }
          else if (combo_response_format.Text == "xml")
          {
            XmlSerializer mySerializer = new XmlSerializer(typeof(model_markups_response_v1));
            tServerResponse = (model_markups_response_v1)mySerializer.Deserialize((new StringReader(tResponse.responseBody)));
          }

          model_markup[] tMarkups = tServerResponse.markups;
          if (tMarkups != null)
          {
            // Show the markups
            foreach (model_markup aMarkup in tMarkups)
            {
              ListViewItem markupNewItem = new ListViewItem(aMarkup.name);
              markupNewItem.SubItems.Add(new ListViewItem.ListViewSubItem(markupNewItem, aMarkup.created_by));
              markupNewItem.SubItems.Add(new ListViewItem.ListViewSubItem(markupNewItem, aMarkup.created_date));
              lvMarkups.Items.Add(markupNewItem);
            }
          }
        }
      }

      ResponseTextBox.Text = tResponse.verboseResponse;
    }

    private void butAggregate_Click(object sender, EventArgs e)
    {
      string timestamp = GetUNIXEpochTimestamp().ToString();
      string postBody = "";
      postBody += "&company_id=" + HttpUtility.UrlEncode(txt_company_id.Text);
      postBody += "&api_key=" + HttpUtility.UrlEncode(txt_api_key.Text);
      postBody += "&auth_token=" + HttpUtility.UrlEncode(txt_auth_token.Text);
      postBody += "&timestamp=" + HttpUtility.UrlEncode(timestamp);
      postBody += "&sig=" + HttpUtility.UrlEncode(GenerateAPISignature(timestamp));

      // Get the model 
      foreach (ListViewItem selectedItem in lvModelModels.SelectedItems)
      {
        postBody += "&model_id=" + selectedItem.SubItems[2].Text;
      }

      if (chk_pretty.Checked)
      {
        postBody += "&pretty=1";
      }
      if (chk_no_http_status.Checked)
      {
        postBody += "&no_http_status=1";
      }

      lvMarkups.Items.Clear();
      glueResponse tResponse = MakeAPICall("model/v1/aggregate", "", "POST", postBody);
      if ((tResponse.statusCode == HttpStatusCode.OK) && (!chk_no_http_status.Checked))
      {
        if (tResponse.responseBody != "")
        {
        }
      }

      ResponseTextBox.Text = tResponse.verboseResponse;

    }

    private void butDeleteModel_Click(object sender, EventArgs e)
    {
      if (MessageBox.Show("Are you sure you want to delete the model?", "Confirm Delete", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.No)
      {
        return;
      }

      string timestamp = GetUNIXEpochTimestamp().ToString();
      string postBody = "";
      postBody += "&company_id=" + HttpUtility.UrlEncode(txt_company_id.Text);
      postBody += "&api_key=" + HttpUtility.UrlEncode(txt_api_key.Text);
      postBody += "&auth_token=" + HttpUtility.UrlEncode(txt_auth_token.Text);
      postBody += "&timestamp=" + HttpUtility.UrlEncode(timestamp);
      postBody += "&sig=" + HttpUtility.UrlEncode(GenerateAPISignature(timestamp));

      // Get the model 
      foreach (ListViewItem selectedItem in lvModelModels.SelectedItems)
      {
        postBody += "&model_id=" + selectedItem.SubItems[2].Text;
      }

      if (chk_pretty.Checked)
      {
        postBody += "&pretty=1";
      }
      if (chk_no_http_status.Checked)
      {
        postBody += "&no_http_status=1";
      }

      lvMarkups.Items.Clear();
      glueResponse tResponse = MakeAPICall("model/v1/delete", "", "POST", postBody);
      if ((tResponse.statusCode == HttpStatusCode.OK) && (!chk_no_http_status.Checked))
      {
        if (tResponse.responseBody != "")
        {
        }
      }

      ResponseTextBox.Text = tResponse.verboseResponse;
    }

    private void butSetModel_Click(object sender, EventArgs e)
    {
      txt_move_model.Text = "";
      if (tvProject.SelectedNode.Tag.ToString().IndexOf("FOLDER") < 0)
      {
        txt_move_model.Text = tvProject.SelectedNode.Name;
      }
    }

    private void butSetFolder_Click(object sender, EventArgs e)
    {
      txt_move_folder.Text = "";
      if (tvProject.SelectedNode.Tag.ToString().IndexOf("FOLDER") >= 0)
      {
        txt_move_folder.Text = tvProject.SelectedNode.Name;
      }
    }

    private void butMoveModel_Click(object sender, EventArgs e)
    {
      string timestamp = GetUNIXEpochTimestamp().ToString();
      string postBody = "";
      postBody += "&company_id=" + HttpUtility.UrlEncode(txt_company_id.Text);
      postBody += "&api_key=" + HttpUtility.UrlEncode(txt_api_key.Text);
      postBody += "&auth_token=" + HttpUtility.UrlEncode(txt_auth_token.Text);
      postBody += "&timestamp=" + HttpUtility.UrlEncode(timestamp);
      postBody += "&sig=" + HttpUtility.UrlEncode(GenerateAPISignature(timestamp));

      // Get the model and folder
      postBody += "&model_id=" + txt_move_model.Text;
      postBody += "&dest_folder_id=" + txt_move_folder.Text;

      if (chk_pretty.Checked)
      {
        postBody += "&pretty=1";
      }
      if (chk_no_http_status.Checked)
      {
        postBody += "&no_http_status=1";
      }

      lvMarkups.Items.Clear();
      glueResponse tResponse = MakeAPICall("model/v1/move", "", "POST", postBody);
      if ((tResponse.statusCode == HttpStatusCode.OK) && (!chk_no_http_status.Checked))
      {
        if (tResponse.responseBody != "")
        {
        }
      }

      ResponseTextBox.Text = tResponse.verboseResponse;
    }

    private void butView_Click(object sender, EventArgs e)
    {
      if (txt_model_action_id.Text == "")
      {
        MessageBox.Show("Please select a model with a valid Action ID.", "Action ID Required", MessageBoxButtons.OK, MessageBoxIcon.Information);
        return;
      }

      ShowModel(txt_model_action_id.Text);
    }

    public void ShowModel(string aActionID)
    {
      if (gViewer == null)
      {
        gViewer = new BIM360DisplayComponent();
      }
      // Build the URL to view the model
      string timestamp = GetUNIXEpochTimestamp().ToString();
      string tURL = "";
      tURL += txt_viewer_base_url.Text;

      // Add question mark 
      if (tURL.Substring(tURL.Length - 1) != "?")
      {
        tURL += "?";
      }

      
      tURL += "api_key=" + txt_api_key.Text;
      tURL += "&timestamp=" + timestamp;
      tURL += "&sig=" + HttpUtility.UrlEncode(GenerateAPISignature(timestamp));
      tURL += "&company_id=" + txt_company_id.Text;
      tURL += "&auth_token=" + txt_auth_token.Text;
      tURL += "&runner=embedded/#autodesk/action/" + aActionID;

//      tURL += "&action_id=" + aActionID;
  //    tURL += "&gui=nav";

      gViewer.isDownloadOperation = false;
      gViewer.Show();
      gViewer.webBrowser.Url = new System.Uri(tURL);
    }

    private void ModelViews_ItemClick(Object sender, ListViewItemSelectionChangedEventArgs e)
    {
      if (e.IsSelected)
      {
        txt_model_action_id.Text = this.lvViews.Items[e.ItemIndex].SubItems[4].Text;
      }
    }

    private void butDownload_Click(object sender, EventArgs e)
    {
      if (chk_head_only.Checked)
      {
        string tURL = buildDownloadURL(txt_model_model_id.Text, txt_model_project_id.Text, true);
        glueResponse tResponse = MakeAPICall(tURL, "", "HEAD");
        if (tResponse.statusCode == HttpStatusCode.OK)
        {
        }
        else
        {
        }

        ResponseTextBox.Text = tResponse.verboseResponse;        
      }
      else if ((num_bytes_start.Value > 0) || (num_bytes_end.Value > 0))
      {
        string tURL = buildDownloadURL(txt_model_model_id.Text, txt_model_project_id.Text, true);
        glueResponse tResponse = MakeAPICall(tURL, "", "GET", "", (int)num_bytes_start.Value, (int) num_bytes_end.Value);
        if (tResponse.statusCode == HttpStatusCode.OK)
        {
        }
        else
        {
        }

        ResponseTextBox.Text = tResponse.verboseResponse;
      }
      else
      {
        if (gViewer == null)
        {
          gViewer = new BIM360DisplayComponent();
        }
        gViewer.isDownloadOperation = true;
        //gViewer.Show();
        gViewer.webBrowser.Url = new System.Uri(txt_model_download_url.Text);
      }
    }

    private void butClearMergedModels_Click(object sender, EventArgs e)
    {
      lvMergedModelComponents.Items.Clear();
    }

    private void butAddMergedModel_Click(object sender, EventArgs e)
    {
      ListViewItem newItem = new ListViewItem(txt_model_model_id.Text);
      lvMergedModelComponents.Items.Add(newItem); //Adding the parent item to the listview control
    }

    private void butMergeModels_Click(object sender, EventArgs e)
    {
      string modelName = Microsoft.VisualBasic.Interaction.InputBox("Enter a name for the merged model:", "Merged Model Name", "");
      if (modelName == "")
      {
        MessageBox.Show("Please enter a valid name for the Merged Model.", "Merged Model Name Required", MessageBoxButtons.OK, MessageBoxIcon.Information);
        return;
      }

      string modelIDs = "";
      foreach (ListViewItem itemRow in this.lvMergedModelComponents.Items)
      {
        if (modelIDs != "")
        {
          modelIDs += ";";
        }
        modelIDs += itemRow.Text;
      }

      string timestamp = GetUNIXEpochTimestamp().ToString();
      string postBody = "";
      postBody += "login_name=" + HttpUtility.UrlEncode(txt_login_name.Text);
      postBody += "&password=" + HttpUtility.UrlEncode(txt_password.Text);
      postBody += "&company_id=" + HttpUtility.UrlEncode(txt_company_id.Text);
      postBody += "&api_key=" + HttpUtility.UrlEncode(txt_api_key.Text);
      postBody += "&auth_token=" + HttpUtility.UrlEncode(txt_auth_token.Text);
      postBody += "&timestamp=" + HttpUtility.UrlEncode(timestamp);
      postBody += "&sig=" + HttpUtility.UrlEncode(GenerateAPISignature(timestamp));
      if (chk_pretty.Checked)
      {
        postBody += "&pretty=1";
      }

      if (chk_no_http_status.Checked)
      {
        postBody += "&no_http_status=1";
      }

      // Specific arguments...
      postBody += "&project_id=" + HttpUtility.UrlEncode(txt_model_project_id.Text);
      postBody += "&model_name=" + HttpUtility.UrlEncode(modelName);
      postBody += "&model_id_list=" + HttpUtility.UrlEncode(modelIDs);

      glueResponse tResponse = MakeAPICall("model/v1/merge", "", "POST", postBody);
      if ((tResponse.statusCode == HttpStatusCode.OK) && (!chk_no_http_status.Checked))
      {
        if (tResponse.responseBody != "")
        {
          if (combo_response_format.Text == "json")
          {
            JavaScriptSerializer jSer = new JavaScriptSerializer();
            model_info_response_v1 tJSON = jSer.Deserialize<model_info_response_v1>(tResponse.responseBody);
          }
          else if (combo_response_format.Text == "xml")
          {
            XmlSerializer mySerializer = new XmlSerializer(typeof(security_login_response_v1));
            model_info_response_v1 tXML = (model_info_response_v1)mySerializer.Deserialize((new StringReader(tResponse.responseBody)));
          }
        }
      }

      ResponseTextBox.Text = tResponse.verboseResponse;
    }


    #endregion Model Group

    #region File Upload Functionality 

    private void butFindFile_Click(object sender, EventArgs e)
    {
      if (openFileDialog.ShowDialog() == DialogResult.OK)
      {
        txt_model_upload.Text = openFileDialog.FileName;
      }
    }

    string getFileName(string aFileName)
    {
      string fileName = aFileName.Substring(aFileName.LastIndexOf('\\') + 1);
      return fileName;
    }

    void writeBufferToDisk(byte[] aBuffer, string aFileName)
    {
      FileStream fs = File.OpenWrite(aFileName);
      fs.Write(aBuffer, 0, aBuffer.Length);
      fs.Close();
    }

    // Function to get byte array from a file
    public byte[] readFileToBuffer(string aFileName)
    {
      byte[] tBuffer = null;

      try
      {
        // Open file for reading
        System.IO.FileStream tFileStream = new System.IO.FileStream(aFileName, System.IO.FileMode.Open, System.IO.FileAccess.Read);

        // attach filestream to binary reader
        System.IO.BinaryReader binReader = new System.IO.BinaryReader(tFileStream);

        // get total byte length of the file
        long totalBytes = new System.IO.FileInfo(aFileName).Length;

        // read entire file into buffer
        tBuffer = binReader.ReadBytes((Int32)totalBytes);

        // close file reader
        tFileStream.Close();
        tFileStream.Dispose();
        binReader.Close();
      }
      catch (Exception ex)
      {
        // Error
        Console.WriteLine("Exception caught in process: {0}", ex.ToString());
      }

      return tBuffer;
    }

    glueResponse MakeFilePostAPICall(string urlEndpoint, string aFileNameOnly, byte[] aBuffer, int chunk_number, int chunk_total)
		{
      // Busy...
      Cursor.Current = Cursors.WaitCursor;

      glueResponse rObj = new glueResponse();
      string reponseAsString = "";
      var url = txt_base_url.Text;
      if (txt_base_url.Text.Substring(txt_base_url.Text.Length - 1) != "/")
      {
        url += "/";
      }
      url += urlEndpoint; // +"." + combo_response_format.Text;

      try
      {
        System.Net.ServicePointManager.Expect100Continue = false;
        HttpWebRequest oRequest = null;
        oRequest = (HttpWebRequest)HttpWebRequest.Create(url);
        oRequest.ContentType = "multipart/form-data; boundary=" + MultipartPostData.boundary;
        oRequest.Method = "POST";

        // Setup the user agent
        oRequest.UserAgent = "User-Agent: APP_NAME/APP_VERSION (platform=APP_PLATFORM, page=APP_PAGE, info=APP_INFO)";

        // Bump up timeout for debugging purposes (5 mins)
        oRequest.Timeout = 300000;

        MultipartPostData pData = new MultipartPostData();
        Encoding encoding = Encoding.UTF8;
        Stream oStream = null;

        // Setup the post args
        string timestamp = GetUNIXEpochTimestamp().ToString();
        pData.Params.Add(new PostDataParam("format", HttpUtility.UrlEncode(combo_response_format.Text), PostDataParamType.Field));
        pData.Params.Add(new PostDataParam("company_id", HttpUtility.UrlEncode(txt_company_id.Text), PostDataParamType.Field));
        pData.Params.Add(new PostDataParam("api_key", HttpUtility.UrlEncode(txt_api_key.Text)           , PostDataParamType.Field));
        pData.Params.Add(new PostDataParam("auth_token", HttpUtility.UrlEncode(txt_auth_token.Text)     , PostDataParamType.Field));
        pData.Params.Add(new PostDataParam("timestamp", HttpUtility.UrlEncode(timestamp)                , PostDataParamType.Field));
        pData.Params.Add(new PostDataParam("sig", HttpUtility.UrlEncode(GenerateAPISignature(timestamp)), PostDataParamType.Field));

        pData.Params.Add(new PostDataParam("project_id", HttpUtility.UrlEncode(txt_project_id.Text), PostDataParamType.Field));
        pData.Params.Add(new PostDataParam("dest_folder_id", HttpUtility.UrlEncode(txt_project_object_id.Text), PostDataParamType.Field));

        if (chk_pretty.Checked)
        {
          pData.Params.Add(new PostDataParam("pretty", "1", PostDataParamType.Field));
        }
        if (chk_no_http_status.Checked)
        {
          pData.Params.Add(new PostDataParam("no_http_status", "1", PostDataParamType.Field));
        }

        pData.Params.Add(new PostDataParam("chunk_number", chunk_number.ToString(), PostDataParamType.Field));
        pData.Params.Add(new PostDataParam("chunk_total", chunk_total.ToString(), PostDataParamType.Field));

        // Send the chunk
        pData.Params.Add(new PostDataParam("chunk", aFileNameOnly, aBuffer, PostDataParamType.File));

        byte[] buffer = pData.GetPostData();
        oRequest.ContentLength = buffer.Length;
        oStream = oRequest.GetRequestStream();
        oStream.Write(buffer, 0, buffer.Length);
        oStream.Close();

        HttpWebResponse oResponse = (HttpWebResponse)oRequest.GetResponse();

        string tHeader = GetHeaderFromResponse(oResponse);
        string tBody = GetBodyFromResponse(oResponse);

        reponseAsString = tHeader + tBody;
        rObj.responseBody = tBody;
        rObj.statusCode = oResponse.StatusCode;

        oResponse.Close();
      }
      catch (WebException ex)
      {
        if (((HttpWebResponse)ex.Response) == null)
        {
          rObj.statusCode = HttpStatusCode.Ambiguous;
        }
        else
        {
          rObj.statusCode = ((HttpWebResponse)ex.Response).StatusCode;
        }
        reponseAsString += handleError(ex);
      }

      rObj.verboseResponse = reponseAsString;

      // Not busy...
      Cursor.Current = Cursors.Default;
      return rObj;
    }

    private void butUpload_Click(object sender, EventArgs e)
    {
      if (txt_model_upload.Text == "")
      {
        MessageBox.Show("Select a file to upload to the BIM 360 Glue Platform.", "BIM 360 Glue API Input Required", MessageBoxButtons.OK, MessageBoxIcon.Information);
        return;
      }

      ResponseTextBox.Text = "Starting upload...\r\n";

      int CHUNK_SIZE = 2000000;
      string fileNameOnly = HttpUtility.UrlEncode(getFileName(txt_model_upload.Text));

      byte[] fileBuffer = readFileToBuffer(txt_model_upload.Text);
      long totalFileBytes = new System.IO.FileInfo(txt_model_upload.Text).Length;

      glueResponse tResponse = null;
      long sentCounter = 0;
      long currentOffset = 0;
      int chunk_number = 1;
      int chunk_total = Convert.ToInt32(((float)totalFileBytes / (float)CHUNK_SIZE) + 0.5);
      if (chunk_total == 0) { chunk_total = 1; }
      for (int i = 0; i < totalFileBytes; i += CHUNK_SIZE)
      {
        long sizeLeft = totalFileBytes - sentCounter;
        long bytesToSend;
        if (sizeLeft > CHUNK_SIZE)
        {
          bytesToSend = CHUNK_SIZE;
        }
        else
        {
          bytesToSend = sizeLeft;
        }

        byte[] newBuffer = new byte[bytesToSend];
        Buffer.BlockCopy(fileBuffer, (int)currentOffset, newBuffer, 0, (int)bytesToSend);

        tResponse = MakeFilePostAPICall("model/v1/upload", fileNameOnly, newBuffer, chunk_number, chunk_total);
        ResponseTextBox.Text = "\r\n" + tResponse.verboseResponse + "\r\n=============================\r\n" + ResponseTextBox.Text + "\r\n";

        if (tResponse.statusCode != HttpStatusCode.OK)
        {
          MessageBox.Show("An error was encountered on this chunk:\n" + tResponse.verboseResponse, "BIM 360 Glue API Error", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        currentOffset += bytesToSend;
        sentCounter += bytesToSend;
        chunk_number++;
      }

      string saveResponseText = ResponseTextBox.Text;

      //rhp: Leave file name for repeated uploads
      //txt_model_upload.Text = "";
      ShowProjectInfo(txt_project_id.Text);

      ResponseTextBox.Text = saveResponseText + "\r\n" + ResponseTextBox.Text + "\r\n";
    }

    #endregion File Upload Functionality

    private void butGetSets_Click(object sender, EventArgs e)
    {
        string timestamp = GetUNIXEpochTimestamp().ToString();
        string callArgs = "";
        callArgs += "&company_id=" + HttpUtility.UrlEncode(txt_company_id.Text);
        callArgs += "&api_key=" + HttpUtility.UrlEncode(txt_api_key.Text);
        callArgs += "&auth_token=" + HttpUtility.UrlEncode(txt_auth_token.Text);
        callArgs += "&timestamp=" + HttpUtility.UrlEncode(timestamp);
        callArgs += "&sig=" + HttpUtility.UrlEncode(GenerateAPISignature(timestamp));
        callArgs += "&model_id=" + txt_equip_model_id.Text;
        callArgs += "&version_id=" + txt_equip_model_version_id.Text;

        if (chk_pretty.Checked)
        {
            callArgs += "&pretty=1";
        }
        if (chk_no_http_status.Checked)
        {
            callArgs += "&no_http_status=1";
        }

        lvMarkups.Items.Clear();
        glueResponse tResponse = MakeAPICall("model/v1/sets", callArgs, "GET", "");
        if ((tResponse.statusCode == HttpStatusCode.OK) && (!chk_no_http_status.Checked))
        {
            if (tResponse.responseBody != "")
            {
                JavaScriptSerializer jSer = new JavaScriptSerializer();
                model_equipment_set_response modResp = jSer.Deserialize<model_equipment_set_response>(tResponse.responseBody);

                rnListView.Items.Clear();
                foreach (model_equipment_set tEquip in modResp.equipment_sets)
                {
                    ListViewItem newItem = new ListViewItem(tEquip.id);
                    rnListView.Items.Add(newItem); //Adding the parent item to the listview control
                }
            }
        }

        ResponseTextBox.Text = tResponse.verboseResponse;
    }

    private void butGetSetObjects_Click(object sender, EventArgs e)
    {
        if (rnListView.SelectedItems.Count <= 0)
        {
            return;
        }

        string timestamp = GetUNIXEpochTimestamp().ToString();
        string callArgs = "";
        callArgs += "&company_id=" + HttpUtility.UrlEncode(txt_company_id.Text);
        callArgs += "&api_key=" + HttpUtility.UrlEncode(txt_api_key.Text);
        callArgs += "&auth_token=" + HttpUtility.UrlEncode(txt_auth_token.Text);
        callArgs += "&timestamp=" + HttpUtility.UrlEncode(timestamp);
        callArgs += "&sig=" + HttpUtility.UrlEncode(GenerateAPISignature(timestamp));
        callArgs += "&model_id=" + txt_equip_model_id.Text;
        callArgs += "&version_id=" + txt_equip_model_version_id.Text;
        callArgs += "&set_id=" + HttpUtility.UrlEncode(rnListView.SelectedItems[0].SubItems[0].Text);
        callArgs += "&chunk=" + num_chunk.Value.ToString();
        callArgs += "&chunk_size=" + num_chunk_size.Value.ToString();
        if (chk_pretty.Checked)
        {
            callArgs += "&pretty=1";
        }
        if (chk_no_http_status.Checked)
        {
            callArgs += "&no_http_status=1";
        }

        lvMarkups.Items.Clear();
        glueResponse tResponse = MakeAPICall("model/v1/set_objects", callArgs, "GET", "");
        if ((tResponse.statusCode == HttpStatusCode.OK) && (!chk_no_http_status.Checked))
        {
            if (tResponse.responseBody != "")
            {
            }
        }

        ResponseTextBox.Text = tResponse.verboseResponse;
    }

   

  }
}
