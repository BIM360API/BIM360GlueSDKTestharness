// Copyright 2012 Autodesk, Inc.  All rights reserved.
// Use of this software is subject to the terms of the Autodesk license agreement 
// provided at the time of installation or download, or which otherwise accompanies 
// this software in either electronic or hard copy form.   

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace BIM360SDKTestClient
{
  public partial class BIM360DisplayComponent : Form
  {
    public Boolean isDownloadOperation = false;
    public BIM360DisplayComponent()
    {
      InitializeComponent();
    }

    void BIM360Viewer_Load(object sender, EventArgs e)
    {
      // Event Handlers...
      this.Closing += new System.ComponentModel.CancelEventHandler(this.BIM360Viewer_Closing);
    }

    private void BIM360Viewer_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
      e.Cancel = true;
      this.Hide();
    }

    private void webBrowser_ProgressChanged(object sender, WebBrowserProgressChangedEventArgs e)
    {
      // int pct = Convert.ToInt16((e.CurrentProgress / e.MaximumProgress) * 100);
      // toolStripStatusLabel1.Text = "Progress " + pct.ToString() + "...";
    }

    private void webBrowser_Navigated(object sender, WebBrowserNavigatedEventArgs e)
    {
      // toolStripStatusLabel1.Text = "Navigation Complete";
    }

    private void webBrowser_Navigating(object sender, WebBrowserNavigatingEventArgs e)
    {
      startBusyIndicator();
      toolStripStatusLabel1.Text = "Loading...";
    }

    private void webBrowser_DocumentCompleted(object sender, WebBrowserDocumentCompletedEventArgs e)
    {
      endBusyIndicator();
      toolStripStatusLabel1.Text = "Complete";
    }

    private void webBrowser_FileDownload(object sender, EventArgs e)
    {
      if (isDownloadOperation)
      {
        toolStripStatusLabel1.Text = "File Downloaded";
        this.Hide();
        endBusyIndicator();
      }
    }

    private void startBusyIndicator()
    {
      panelBusy.Location = new Point(Convert.ToInt32( (this.Width / 2) - (panelBusy.Width / 2)), 35);
      panelBusy.Show();
    }

    private void endBusyIndicator()
    {
      panelBusy.Hide();
    }

  }
}
