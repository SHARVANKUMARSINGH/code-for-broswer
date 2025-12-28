using Microsoft.Web.WebView2.WinForms;
using Microsoft.Web.WebView2.Core;
using System.Net.NetworkInformation;
using System.Drawing.Drawing2D;
using System.Reflection;

namespace VantaBrowser; 

public class BrowserForm : Form
{
    private TabControl tabControl = null!;
    private Panel toolbar = null!;
    private TextBox txtAddress = null!;
    private Label lblPing = null!;
    private Label lblDataSaver = null!;
    private System.Windows.Forms.Timer pingTimer = null!;
    private ContextMenuStrip vantaContextMenu = null!;
    
    private bool isDataSaverOn = false;
    private string homeHtmlContent = ""; 

    private Color ColorVanta = Color.FromArgb(10, 10, 12);     
    private Color ColorToolbar = Color.FromArgb(20, 20, 25);   
    private Color ColorNeon = Color.FromArgb(0, 255, 213);     
    private Color ColorText = Color.White;
    private Color ColorClose = Color.FromArgb(255, 60, 60); 

    public BrowserForm()
    {
        LoadEmbeddedHtml();

        this.Text = "VANTA Browser"; 
        this.Size = new Size(1280, 800);
        this.StartPosition = FormStartPosition.CenterScreen;
        this.BackColor = ColorVanta;
        this.ForeColor = ColorText;
        this.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);

        InitializeCustomUI();
        InitializeContextMenu();

        pingTimer = new System.Windows.Forms.Timer();
        pingTimer.Interval = 3000; 
        pingTimer.Tick += async (s, e) => await UpdatePing();
        pingTimer.Start();

        AddNewTab(); 
    }

    private void LoadEmbeddedHtml()
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            // Tries to load VantaBrowser.home.html or NoxBrowser.home.html just in case
            using (Stream? stream = assembly.GetManifestResourceStream("VantaBrowser.home.html") ?? assembly.GetManifestResourceStream("NoxBrowser.home.html"))
            {
                if (stream != null)
                {
                    using (StreamReader reader = new StreamReader(stream))
                        homeHtmlContent = reader.ReadToEnd();
                }
            }
        }
        catch { homeHtmlContent = "<h1 style='color:white'>Error loading home page</h1>"; }
    }

    private void InitializeContextMenu()
    {
        vantaContextMenu = new ContextMenuStrip();
        vantaContextMenu.BackColor = ColorToolbar;
        vantaContextMenu.ForeColor = ColorText;
        vantaContextMenu.ShowImageMargin = false; 
        vantaContextMenu.RenderMode = ToolStripRenderMode.System;

        var itemBack = vantaContextMenu.Items.Add("Start");
        itemBack.Click += (s, e) => { var wv = GetCurrentWebView(); if (wv != null && wv.CanGoBack) wv.GoBack(); };
        
        var itemForward = vantaContextMenu.Items.Add("Next");
        itemForward.Click += (s, e) => { var wv = GetCurrentWebView(); if (wv != null && wv.CanGoForward) wv.GoForward(); };

        var itemReload = vantaContextMenu.Items.Add("Reload");
        itemReload.Click += (s, e) => { GetCurrentWebView()?.Reload(); };

        vantaContextMenu.Items.Add(new ToolStripSeparator());

        var itemCopy = vantaContextMenu.Items.Add("Copy Link");
        itemCopy.Click += (s, e) => { 
            var url = GetCurrentWebView()?.Source.ToString();
            if(url != null) Clipboard.SetText(url); 
        };
    }

    private void InitializeCustomUI()
    {
        toolbar = new Panel { Dock = DockStyle.Top, Height = 60, BackColor = ColorToolbar, Padding = new Padding(5) };

        Button btnBack = CreateStyledButton("<", (s, e) => { var wv = GetCurrentWebView(); if (wv != null && wv.CanGoBack) wv.GoBack(); });
        Button btnForward = CreateStyledButton(">", (s, e) => { var wv = GetCurrentWebView(); if (wv != null && wv.CanGoForward) wv.GoForward(); });
        Button btnRefresh = CreateStyledButton("⟳", (s, e) => { var wv = GetCurrentWebView(); if (wv != null) wv.Reload(); });
        Button btnNewTab = CreateStyledButton("+", (s, e) => AddNewTab());

        lblDataSaver = new Label { Text = "⚡ Saver: OFF", ForeColor = Color.Gray, AutoSize = false, Width = 110, TextAlign = ContentAlignment.MiddleCenter, Dock = DockStyle.Right, Cursor = Cursors.Hand, Font = new Font("Segoe UI", 10, FontStyle.Bold) };
        lblDataSaver.Click += ToggleDataSaver;

        lblPing = new Label { Text = "Ping: --", ForeColor = ColorNeon, AutoSize = false, Width = 100, TextAlign = ContentAlignment.MiddleCenter, Dock = DockStyle.Right, Font = new Font("Consolas", 10, FontStyle.Bold) };

        Panel addressContainer = new Panel { Dock = DockStyle.Fill, Padding = new Padding(15, 12, 15, 5), BackColor = ColorToolbar };
        txtAddress = new TextBox { Dock = DockStyle.Top, BackColor = Color.FromArgb(15, 15, 20), ForeColor = ColorNeon, BorderStyle = BorderStyle.None, Font = new Font("Segoe UI", 12), TextAlign = HorizontalAlignment.Center };
        txtAddress.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { Navigate(); e.SuppressKeyPress = true; } };
        
        Panel underline = new Panel { Height = 2, Dock = DockStyle.Bottom, BackColor = ColorNeon, Margin = new Padding(0, 5, 0, 0) };
        addressContainer.Controls.Add(txtAddress);
        addressContainer.Controls.Add(underline);

        toolbar.Controls.Add(addressContainer);
        toolbar.Controls.Add(lblPing);
        toolbar.Controls.Add(lblDataSaver);
        toolbar.Controls.Add(btnNewTab);
        toolbar.Controls.Add(btnRefresh);
        toolbar.Controls.Add(btnForward);
        toolbar.Controls.Add(btnBack);

        tabControl = new TabControl { Dock = DockStyle.Fill, DrawMode = TabDrawMode.OwnerDrawFixed, Padding = new Point(25, 8), ItemSize = new Size(140, 30) };
        tabControl.DrawItem += DrawCustomTab;
        tabControl.SelectedIndexChanged += (s, e) => UpdateAddressBar();
        tabControl.MouseDown += TabControl_MouseDown; 

        this.Controls.Add(tabControl);
        this.Controls.Add(toolbar);
    }

    private void AddNewTab(string? startUrl = null)
    {
        TabPage page = new TabPage("Home");
        page.BackColor = ColorVanta;
        WebView2 webView = new WebView2 { Dock = DockStyle.Fill };
        
        webView.EnsureCoreWebView2Async().ContinueWith((task) =>
        {
            Invoke((MethodInvoker)delegate
            {
                var settings = webView.CoreWebView2.Settings;
                settings.AreDefaultContextMenusEnabled = false; 
                settings.IsStatusBarEnabled = false;            
                settings.AreDevToolsEnabled = false;            
                settings.IsZoomControlEnabled = false;          
                settings.IsBuiltInErrorPageEnabled = false;     
                settings.IsPasswordAutosaveEnabled = false;     
                settings.IsGeneralAutofillEnabled = false;      
                settings.IsSwipeNavigationEnabled = false;      
                settings.UserAgent = "VANTA/1.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/110.0.0.0 Safari/537.36";

                webView.CoreWebView2.NewWindowRequested += (s, e) => { e.Handled = true; AddNewTab(e.Uri); };

                // FORCE TAB NAME TO "HOME" ALWAYS
                webView.CoreWebView2.DocumentTitleChanged += (s, e) => 
                {
                    string title = webView.CoreWebView2.DocumentTitle;
                    if (string.IsNullOrEmpty(title) || title.Contains("Nox") || title.Contains("Vanta") || title.Contains(".html"))
                        page.Text = "Home";
                    else
                        page.Text = title;
                };

                webView.CoreWebView2.ContextMenuRequested += (s, args) => { args.Handled = true; vantaContextMenu.Show(Cursor.Position); };

                webView.CoreWebView2.DownloadStarting += (sender, args) =>
                {
                    args.Handled = true; 
                    SaveFileDialog saveDialog = new SaveFileDialog();
                    saveDialog.FileName = args.ResultFilePath;
                    if (saveDialog.ShowDialog() == DialogResult.OK) args.ResultFilePath = saveDialog.FileName;
                    else args.Cancel = true;
                };

                webView.NavigationCompleted += (s, e) => 
                {
                    if (!e.IsSuccess)
                    {
                        string errorHtml = "<html><body style='background-color:#0A0A0C; color:white; font-family:sans-serif; text-align:center; padding-top:100px;'><h1 style='color:#00FFD5; font-size:50px;'>VANTA DISCONNECTED</h1><h3>Connection Failed.</h3></body></html>";
                        webView.NavigateToString(errorHtml);
                    }
                };

                webView.CoreWebView2.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.Image);
                webView.CoreWebView2.WebResourceRequested += (sender, args) =>
                {
                    if (isDataSaverOn) args.Response = webView.CoreWebView2.Environment.CreateWebResourceResponse(null, 403, "Blocked", "");
                };

                if (startUrl != null) webView.CoreWebView2.Navigate(startUrl);
                else if (!string.IsNullOrEmpty(homeHtmlContent)) webView.NavigateToString(homeHtmlContent);
                else webView.CoreWebView2.Navigate("https://www.google.com");
                
                webView.SourceChanged += (s, e) => UpdateAddressBar();
            });
        });

        page.Controls.Add(webView);
        tabControl.TabPages.Add(page);
        tabControl.SelectedTab = page;
    }

    private WebView2? GetCurrentWebView() => tabControl.SelectedTab?.Controls.Count > 0 ? tabControl.SelectedTab.Controls[0] as WebView2 : null;

    private void Navigate()
    {
        var webView = GetCurrentWebView();
        if (webView?.CoreWebView2 == null) return;
        string input = txtAddress.Text.Trim();
        if (string.IsNullOrWhiteSpace(input) || input == "Home") return; 

        if (!input.Contains(".") || input.Contains(" ")) webView.CoreWebView2.Navigate($"https://www.google.com/search?q={Uri.EscapeDataString(input)}");
        else { if (!input.StartsWith("http")) input = "https://" + input; webView.CoreWebView2.Navigate(input); }
    }

    private void UpdateAddressBar()
    {
        var webView = GetCurrentWebView();
        if (webView?.Source != null)
        {
            string url = webView.Source.ToString();
            if (url == "about:blank" || url.StartsWith("data:text/html")) txtAddress.Text = "Home";
            else txtAddress.Text = url;
        }
    }

    private void ToggleDataSaver(object? sender, EventArgs e)
    {
        isDataSaverOn = !isDataSaverOn;
        lblDataSaver.Text = isDataSaverOn ? "⚡ Saver: ON" : "⚡ Saver: OFF";
        lblDataSaver.ForeColor = isDataSaverOn ? ColorNeon : Color.Gray;
        if(GetCurrentWebView()?.CoreWebView2 != null) GetCurrentWebView()?.Reload();
    }

    private async Task UpdatePing()
    {
        try {
            Ping p = new Ping(); PingReply reply = await p.SendPingAsync("8.8.8.8", 1000);
            lblPing.Text = $"Ping: {reply.RoundtripTime}ms";
            lblPing.ForeColor = reply.RoundtripTime < 50 ? ColorNeon : (reply.RoundtripTime < 150 ? Color.Orange : Color.Red);
        } catch { lblPing.Text = "Ping: --"; }
    }

    private Button CreateStyledButton(string text, EventHandler onClick)
    {
        Button btn = new Button { Text = text, Width = 50, Dock = DockStyle.Left, FlatStyle = FlatStyle.Flat, BackColor = ColorToolbar, ForeColor = ColorNeon, Cursor = Cursors.Hand, Font = new Font("Segoe UI", 12, FontStyle.Bold) };
        btn.FlatAppearance.BorderSize = 0; btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(60, 60, 70);
        btn.Click += onClick; return btn;
    }

    private void DrawCustomTab(object? sender, DrawItemEventArgs e)
    {
        var tabs = sender as TabControl;
        if (tabs == null || e.Index >= tabs.TabCount) return;
        
        var page = tabs.TabPages[e.Index];
        var bounds = e.Bounds;
        bool isSelected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
        
        using (SolidBrush brush = new SolidBrush(isSelected ? ColorVanta : ColorToolbar)) 
            e.Graphics.FillRectangle(brush, bounds);

        string tabText = page.Text;
        
        // FINAL FIX: FORCES "Home" DISPLAY
        if(tabText.Contains("Nox") || tabText.Contains("Vanta") || tabText == "") tabText = "Home"; 
        
        if(tabText.Length > 15) tabText = tabText.Substring(0, 12) + "...";

        Rectangle textRect = bounds;
        textRect.Width -= 20; 
        TextRenderer.DrawText(e.Graphics, tabText, this.Font, textRect, isSelected ? ColorNeon : Color.Gray, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

        Rectangle closeRect = new Rectangle(bounds.Right - 20, bounds.Top + 8, 15, 15);
        TextRenderer.DrawText(e.Graphics, "x", new Font("Segoe UI", 10, FontStyle.Bold), closeRect, ColorClose, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

        if (isSelected) using (Pen pen = new Pen(ColorNeon, 3)) e.Graphics.DrawLine(pen, bounds.Left, bounds.Bottom - 2, bounds.Right, bounds.Bottom - 2);
    }

    private void TabControl_MouseDown(object? sender, MouseEventArgs e)
    {
        for (int i = 0; i < tabControl.TabCount; i++)
        {
            Rectangle r = tabControl.GetTabRect(i);
            Rectangle closeRect = new Rectangle(r.Right - 20, r.Top + 8, 15, 15);
            if (closeRect.Contains(e.Location))
            {
                tabControl.TabPages.RemoveAt(i);
                if (tabControl.TabCount == 0) Application.Exit();
                return;
            }
        }
    }
}