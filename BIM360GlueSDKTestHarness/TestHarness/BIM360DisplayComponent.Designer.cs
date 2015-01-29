namespace BIM360SDKTestClient
{
  partial class BIM360DisplayComponent
  {
    /// <summary>
    /// Required designer variable.
    /// </summary>
    private System.ComponentModel.IContainer components = null;

    /// <summary>
    /// Clean up any resources being used.
    /// </summary>
    /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
    protected override void Dispose(bool disposing)
    {
      if (disposing && (components != null))
      {
        components.Dispose();
      }
      base.Dispose(disposing);
    }

    #region Windows Form Designer generated code

    /// <summary>
    /// Required method for Designer support - do not modify
    /// the contents of this method with the code editor.
    /// </summary>
    private void InitializeComponent()
    {
      System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(BIM360DisplayComponent));
      this.webBrowser = new System.Windows.Forms.WebBrowser();
      this.statusStrip1 = new System.Windows.Forms.StatusStrip();
      this.toolStripStatusLabel1 = new System.Windows.Forms.ToolStripStatusLabel();
      this.panelBusy = new System.Windows.Forms.Panel();
      this.pictureBox1 = new System.Windows.Forms.PictureBox();
      this.label1 = new System.Windows.Forms.Label();
      this.statusStrip1.SuspendLayout();
      this.panelBusy.SuspendLayout();
      ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).BeginInit();
      this.SuspendLayout();
      // 
      // webBrowser
      // 
      this.webBrowser.Dock = System.Windows.Forms.DockStyle.Fill;
      this.webBrowser.Location = new System.Drawing.Point(0, 0);
      this.webBrowser.MinimumSize = new System.Drawing.Size(20, 20);
      this.webBrowser.Name = "webBrowser";
      this.webBrowser.Size = new System.Drawing.Size(692, 602);
      this.webBrowser.TabIndex = 0;
      this.webBrowser.DocumentCompleted += new System.Windows.Forms.WebBrowserDocumentCompletedEventHandler(this.webBrowser_DocumentCompleted);
      this.webBrowser.FileDownload += new System.EventHandler(this.webBrowser_FileDownload);
      this.webBrowser.Navigated += new System.Windows.Forms.WebBrowserNavigatedEventHandler(this.webBrowser_Navigated);
      this.webBrowser.Navigating += new System.Windows.Forms.WebBrowserNavigatingEventHandler(this.webBrowser_Navigating);
      this.webBrowser.ProgressChanged += new System.Windows.Forms.WebBrowserProgressChangedEventHandler(this.webBrowser_ProgressChanged);
      // 
      // statusStrip1
      // 
      this.statusStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toolStripStatusLabel1});
      this.statusStrip1.Location = new System.Drawing.Point(0, 580);
      this.statusStrip1.Name = "statusStrip1";
      this.statusStrip1.Size = new System.Drawing.Size(692, 22);
      this.statusStrip1.TabIndex = 1;
      this.statusStrip1.Text = "statusStrip1";
      // 
      // toolStripStatusLabel1
      // 
      this.toolStripStatusLabel1.Name = "toolStripStatusLabel1";
      this.toolStripStatusLabel1.Size = new System.Drawing.Size(0, 17);
      // 
      // panelBusy
      // 
      this.panelBusy.BackColor = System.Drawing.Color.White;
      this.panelBusy.Controls.Add(this.pictureBox1);
      this.panelBusy.Controls.Add(this.label1);
      this.panelBusy.Location = new System.Drawing.Point(166, 34);
      this.panelBusy.Name = "panelBusy";
      this.panelBusy.Size = new System.Drawing.Size(298, 69);
      this.panelBusy.TabIndex = 2;
      // 
      // pictureBox1
      // 
      this.pictureBox1.Image = global::BIM360SDKTestClient.Properties.Resources.busy;
      this.pictureBox1.InitialImage = global::BIM360SDKTestClient.Properties.Resources.busy;
      this.pictureBox1.Location = new System.Drawing.Point(18, 23);
      this.pictureBox1.Name = "pictureBox1";
      this.pictureBox1.Size = new System.Drawing.Size(26, 26);
      this.pictureBox1.TabIndex = 3;
      this.pictureBox1.TabStop = false;
      // 
      // label1
      // 
      this.label1.AutoSize = true;
      this.label1.Font = new System.Drawing.Font("Microsoft Sans Serif", 14.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
      this.label1.ForeColor = System.Drawing.Color.OrangeRed;
      this.label1.Location = new System.Drawing.Point(47, 23);
      this.label1.Name = "label1";
      this.label1.Size = new System.Drawing.Size(232, 24);
      this.label1.TabIndex = 0;
      this.label1.Text = "Loading... Please wait...";
      // 
      // BIM360DisplayComponent
      // 
      this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
      this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
      this.ClientSize = new System.Drawing.Size(692, 602);
      this.Controls.Add(this.panelBusy);
      this.Controls.Add(this.statusStrip1);
      this.Controls.Add(this.webBrowser);
      this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
      this.Name = "BIM360DisplayComponent";
      this.Text = "BIM 360 Glue Display Component";
      this.WindowState = System.Windows.Forms.FormWindowState.Maximized;
      this.Load += new System.EventHandler(this.BIM360Viewer_Load);
      this.statusStrip1.ResumeLayout(false);
      this.statusStrip1.PerformLayout();
      this.panelBusy.ResumeLayout(false);
      this.panelBusy.PerformLayout();
      ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).EndInit();
      this.ResumeLayout(false);
      this.PerformLayout();

    }

    #endregion

    public System.Windows.Forms.WebBrowser webBrowser;
    private System.Windows.Forms.StatusStrip statusStrip1;
    private System.Windows.Forms.ToolStripStatusLabel toolStripStatusLabel1;
    private System.Windows.Forms.Panel panelBusy;
    private System.Windows.Forms.Label label1;
    private System.Windows.Forms.PictureBox pictureBox1;

  }
}