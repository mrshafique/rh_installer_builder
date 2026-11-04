using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Windows.Forms;

// Minimal step wizard: one question per screen, thin stepper on top,
// progress + log on the build step, finish step offers open/start-over/close.
// Zero dependencies: only .NET Framework + csc.exe (both ship with Windows).
public class WizardForm : Form
{
    private static readonly Color Bg = Color.FromArgb(250, 250, 252);
    private static readonly Color Ink = Color.FromArgb(15, 23, 42);
    private static readonly Color Muted = Color.FromArgb(100, 116, 139);
    private static readonly Color Brand = Color.FromArgb(79, 70, 229);
    private static readonly Color BrandDark = Color.FromArgb(67, 56, 202);
    private static readonly Color Line = Color.FromArgb(229, 231, 235);
    private static readonly string[] Steps = { "App", "Files", "Output", "Build", "Done" };

    private int step;
    private Panel stepperBar, pageHost;
    private Label stepTitle, stepSub;
    private Button backBtn, nextBtn;

    // Step 1 fields
    private TextBox appNameBox, versionBox, publisherBox;
    // Step 2 fields
    private TextBox sourceBox;
    private ComboBox mainExeBox;
    // Step 3 fields
    private TextBox outputBox;
    private CheckBox adminCheck, desktopCheck;
    // Step 4 fields
    private ProgressBar bar;
    private Label buildStatus;
    private TextBox logBox;
    private bool built;
    private string builtOutput = "";
    // Step 5 labels
    private Label donePath;

    public WizardForm()
    {
        Text = "RH Installer Builder";
        Width = 560; Height = 560;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Bg;
        Font = new Font("Segoe UI", 9F);
        MinimumSize = new Size(520, 520);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;

        stepperBar = new Panel() { Dock = DockStyle.Top, Height = 64, BackColor = Color.White };
        stepperBar.Paint += PaintStepper;
        Controls.Add(stepperBar);

        var head = new Panel() { Dock = DockStyle.Top, Height = 64, BackColor = Bg };
        stepTitle = new Label() { Left = 24, Top = 10, Width = 500, Font = new Font("Segoe UI", 13F, FontStyle.Bold), ForeColor = Ink };
        stepSub = new Label() { Left = 24, Top = 36, Width = 500, ForeColor = Muted };
        head.Controls.Add(stepTitle); head.Controls.Add(stepSub);
        Controls.Add(head);

        pageHost = new Panel() { Dock = DockStyle.Fill, BackColor = Bg, Padding = new Padding(24, 4, 24, 4) };
        Controls.Add(pageHost);
        pageHost.BringToFront();

        var footer = new Panel() { Dock = DockStyle.Bottom, Height = 60, BackColor = Color.White };
        backBtn = GhostBtn("← Back");
        backBtn.Left = 24; backBtn.Top = 14; backBtn.Width = 110;
        backBtn.Click += (s, e) => { if (step > 0) { step--; Render(); } };
        nextBtn = PrimaryBtn("Next →");
        nextBtn.Left = 426; nextBtn.Top = 14; nextBtn.Width = 110;
        nextBtn.Click += (s, e) => OnNext();
        footer.Controls.Add(backBtn); footer.Controls.Add(nextBtn);
        Controls.Add(footer);
        footer.BringToFront();

        appNameBox = new TextBox() { Text = "Hostel Management" };
        versionBox = new TextBox() { Text = "1.0.0" };
        publisherBox = new TextBox() { Text = "BreezeAI" };
        sourceBox = new TextBox() { Text = "" };
        mainExeBox = new ComboBox() { DropDownStyle = ComboBoxStyle.DropDownList };
        outputBox = new TextBox() { Text = "" };
        adminCheck = new CheckBox() { Text = "Require Administrator (UAC prompt)", Checked = true, Width = 460 };
        desktopCheck = new CheckBox() { Text = "Create Desktop shortcut", Checked = true, Width = 460 };
        bar = new ProgressBar() { Height = 16 };
        buildStatus = new Label() { ForeColor = Muted, Width = 480, Height = 36 };
        logBox = new TextBox()
        {
            Multiline = true, ScrollBars = ScrollBars.Vertical, ReadOnly = true,
            BackColor = Color.FromArgb(15, 23, 42), ForeColor = Color.FromArgb(186, 230, 253),
            Font = new Font("Consolas", 8.5F), BorderStyle = BorderStyle.FixedSingle, Height = 150
        };
        donePath = new Label() { ForeColor = Muted, Width = 480, Height = 40 };

        step = 0;
        Render();
    }

    // ---------------------------------------------------------- stepper UI
    private void PaintStepper(object sender, PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Color.White);
        int n = Steps.Length;
        float w = stepperBar.Width / (float)n;
        for (int i = 0; i < n; i++)
        {
            float cx = w * i + w / 2;
            bool done = i < step;
            bool cur = i == step;
            Color c = done || cur ? Brand : Color.FromArgb(203, 213, 225);
            using (var b = new SolidBrush(c)) g.FillEllipse(b, cx - 11, 10, 22, 22);
            string glyph = done ? "✓" : (i + 1).ToString();
            TextRenderer.DrawText(g, glyph, new Font("Segoe UI", 9F, FontStyle.Bold), new Rectangle((int)cx - 11, 10, 22, 22), Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            TextRenderer.DrawText(g, Steps[i], Font, new Rectangle((int)(w * i), 34, (int)w, 20), cur ? Brand : Muted, TextFormatFlags.HorizontalCenter | TextFormatFlags.Top);
            if (i < n - 1)
            {
                using (var p = new Pen(i < step ? Brand : Line, 2))
                    g.DrawLine(p, cx + 16, 21, cx + w - 16, 21);
            }
        }
    }

    // ---------------------------------------------------------- page render
    private void Render()
    {
        pageHost.Controls.Clear();
        stepperBar.Invalidate();
        backBtn.Enabled = step > 0 && step < 3;
        switch (step)
        {
            case 0:
                stepTitle.Text = "What are we packaging?";
                stepSub.Text = "Step 1 of 5 — application details";
                AddRow("App name", appNameBox);
                AddRow("Version", versionBox);
                AddRow("Publisher", publisherBox);
                nextBtn.Text = "Next →"; nextBtn.Enabled = true;
                break;
            case 1:
                stepTitle.Text = "Where are the files?";
                stepSub.Text = "Step 2 of 5 — pick the Release folder";
                AddRow("Source folder", sourceBox);
                var browse = GhostBtn("Browse...");
                browse.Top = sourceBox.Top; browse.Left = 400; browse.Width = 100;
                browse.Click += (s, e) =>
                {
                    using (var d = new FolderBrowserDialog())
                    {
                        d.Description = "Select app folder (the Release folder with your .exe)";
                        if (d.ShowDialog() == DialogResult.OK)
                        {
                            sourceBox.Text = d.SelectedPath;
                            RefreshExeList();
                            AutoSuggestOutput();
                        }
                    }
                };
                pageHost.Controls.Add(browse);
                var exeLbl = new Label() { Text = "Main .exe", Left = 0, Top = 76, Width = 480, ForeColor = Muted };
                pageHost.Controls.Add(exeLbl);
                mainExeBox.Left = 0; mainExeBox.Top = 98; mainExeBox.Width = 380;
                pageHost.Controls.Add(mainExeBox);
                var refresh = GhostBtn("Refresh");
                refresh.Top = 97; refresh.Left = 390; refresh.Width = 90;
                refresh.Click += (s, e) => RefreshExeList();
                pageHost.Controls.Add(refresh);
                nextBtn.Text = "Next →"; nextBtn.Enabled = true;
                break;
            case 2:
                stepTitle.Text = "Where should Setup go?";
                stepSub.Text = "Step 3 of 5 — output and options";
                AddRow("Output Setup.exe", outputBox);
                var save = GhostBtn("Save as...");
                save.Top = outputBox.Top; save.Left = 400; save.Width = 100;
                save.Click += (s, e) =>
                {
                    using (var dlg = new SaveFileDialog())
                    {
                        dlg.Filter = "Setup exe|*.exe"; dlg.FileName = appNameBox.Text.Trim() + "-Setup.exe";
                        if (dlg.ShowDialog() == DialogResult.OK) outputBox.Text = dlg.FileName;
                    }
                };
                pageHost.Controls.Add(save);
                adminCheck.Top = 80; adminCheck.Left = 0;
                desktopCheck.Top = 106; desktopCheck.Left = 0;
                pageHost.Controls.Add(adminCheck); pageHost.Controls.Add(desktopCheck);
                nextBtn.Text = "Build →"; nextBtn.Enabled = true;
                break;
            case 3:
                stepTitle.Text = "Building your installer";
                stepSub.Text = "Step 4 of 5 — packing and compiling";
                buildStatus.Text = "Press Build to start.";
                buildStatus.Top = 0; buildStatus.Left = 0;
                bar.Top = 42; bar.Left = 0; bar.Width = 488; bar.Value = 0;
                logBox.Top = 68; logBox.Left = 0; logBox.Width = 488;
                pageHost.Controls.Add(buildStatus); pageHost.Controls.Add(bar); pageHost.Controls.Add(logBox);
                nextBtn.Text = "Build";
                backBtn.Enabled = !building;
                break;
            case 4:
                stepTitle.Text = built ? "Done ✓" : "Build the installer first";
                stepSub.Text = "Step 5 of 5 — finish";
                donePath.Text = built ? ("Created:\n" + builtOutput) : "Go back and press Build.";
                donePath.Top = 0; donePath.Left = 0;
                pageHost.Controls.Add(donePath);
                int by = 60;
                by = AddFinishBtn("Open output location", by, () =>
                {
                    try
                    {
                        string dir = Path.GetDirectoryName(builtOutput);
                        if (Directory.Exists(dir)) Process.Start(dir);
                    }
                    catch (Exception ex) { MessageBox.Show("Cannot open folder: " + ex.Message); }
                }, built);
                by = AddFinishBtn("Create new (start over)", by, () => { step = 0; built = false; Render(); }, true);
                by = AddFinishBtn("Close", by, () => Close(), true);
                nextBtn.Text = "Close";
                break;
        }
    }

    private int AddFinishBtn(string text, int y, Action onClick, bool enabled)
    {
        var b = enabled ? PrimaryBtn(text) : GhostBtn(text);
        b.Left = 0; b.Top = y; b.Width = 240; b.Height = 34; b.Enabled = enabled;
        b.Click += (s, e) => onClick();
        pageHost.Controls.Add(b);
        return y + 44;
    }

    private void AddRow(string label, Control field)
    {
        int i = pageHost.Controls.Count / 2;
        var lbl = new Label() { Text = label, Left = 0, Top = i * 68, Width = 480, ForeColor = Muted };
        field.Left = 0; field.Top = i * 68 + 22; field.Width = 380;
        pageHost.Controls.Add(lbl); pageHost.Controls.Add(field);
    }

    private Button PrimaryBtn(string text)
    {
        var b = new Button() { Text = text, Height = 32, FlatStyle = FlatStyle.Flat, BackColor = Brand, ForeColor = Color.White, Cursor = Cursors.Hand, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
        b.FlatAppearance.BorderSize = 0;
        return b;
    }

    private Button GhostBtn(string text)
    {
        var b = new Button() { Text = text, Height = 32, FlatStyle = FlatStyle.Flat, BackColor = Color.White, ForeColor = Brand, Cursor = Cursors.Hand };
        b.FlatAppearance.BorderColor = Color.FromArgb(199, 210, 254);
        return b;
    }

    // ---------------------------------------------------------- navigation
    private void OnNext()
    {
        if (step == 0)
        {
            if (appNameBox.Text.Trim() == "") { MessageBox.Show("Enter an app name."); return; }
            step = 1;
        }
        else if (step == 1)
        {
            if (!Directory.Exists(sourceBox.Text.Trim())) { MessageBox.Show("Pick a valid source folder."); return; }
            if (mainExeBox.Text.Trim() == "" || !File.Exists(Path.Combine(sourceBox.Text.Trim(), mainExeBox.Text.Trim()))) { MessageBox.Show("Select the main .exe."); return; }
            step = 2;
        }
        else if (step == 2)
        {
            if (outputBox.Text.Trim() == "") { MessageBox.Show("Choose the output Setup.exe path."); return; }
            step = 3;
        }
        else if (step == 3)
        {
            Build();
            return;
        }
        else { Close(); return; }
        Render();
    }

    private void RefreshExeList()
    {
        mainExeBox.Items.Clear();
        try
        {
            if (!Directory.Exists(sourceBox.Text.Trim())) return;
            var exes = Directory.GetFiles(sourceBox.Text.Trim(), "*.exe", SearchOption.TopDirectoryOnly);
            foreach (var f in exes) mainExeBox.Items.Add(Path.GetFileName(f));
            if (mainExeBox.Items.Count > 0) mainExeBox.SelectedIndex = 0;
            Log(exes.Length == 0 ? "WARNING: no .exe in that folder." : ("Found " + exes.Length + " exe(s)."));
        }
        catch (Exception ex) { Log("Cannot list exes: " + ex.Message); }
    }

    private void AutoSuggestOutput()
    {
        if (outputBox.Text.Trim() != "") return;
        try
        {
            string safe = appNameBox.Text.Trim().Replace(" ", "");
            if (safe == "") safe = "App";
            outputBox.Text = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                safe + "-Setup-" + versionBox.Text.Trim() + ".exe");
        }
        catch { }
    }

    // ---------------------------------------------------------- build
    private bool building;

    private void Build()
    {
        if (building) return;
        building = true;
        backBtn.Enabled = false; nextBtn.Enabled = false;
        string work = null;
        try
        {
            string appName = appNameBox.Text.Trim();
            string version = versionBox.Text.Trim();
            string publisher = publisherBox.Text.Trim();
            string source = sourceBox.Text.Trim();
            string mainExe = mainExeBox.Text.Trim();
            string output = outputBox.Text.Trim();

            string csc = FindCsc();
            if (csc == null) throw new Exception("C# compiler (csc.exe) not found. .NET Framework is required.");
            Log("Compiler: " + csc);

            work = Path.Combine(Path.GetTempPath(), "InstallerBuilder_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(work);
            bar.Value = 10; buildStatus.Text = "Packing files..."; Application.DoEvents();

            string zip = Path.Combine(work, "app.zip");
            ZipFile.CreateFromDirectory(source, zip, CompressionLevel.Optimal, false);
            Log("Payload: " + (new FileInfo(zip).Length / 1024 / 1024) + " MB");
            bar.Value = 40; buildStatus.Text = "Generating installer..."; Application.DoEvents();

            string setupCs = Path.Combine(work, "Setup.cs");
            File.WriteAllText(setupCs, SetupTemplate(appName, version, publisher, mainExe, desktopCheck.Checked), Encoding.UTF8);

            string args;
            if (adminCheck.Checked)
            {
                string manifest = Path.Combine(work, "admin.manifest");
                File.WriteAllText(manifest, AdminManifest(appName), Encoding.UTF8);
                args = "/target:winexe /out:\"" + output + "\" /win32manifest:\"" + manifest + "\" /res:\"" + zip + "\",app.zip /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.IO.Compression.FileSystem.dll \"" + setupCs + "\"";
            }
            else
            {
                args = "/target:winexe /out:\"" + output + "\" /res:\"" + zip + "\",app.zip /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.IO.Compression.FileSystem.dll \"" + setupCs + "\"";
            }

            string outDir = Path.GetDirectoryName(output);
            if (!string.IsNullOrEmpty(outDir)) Directory.CreateDirectory(outDir);
            bar.Value = 70; buildStatus.Text = "Compiling Setup.exe..."; Application.DoEvents();
            var psi = new ProcessStartInfo(csc, args);
            psi.CreateNoWindow = true; psi.UseShellExecute = false;
            psi.RedirectStandardOutput = true; psi.RedirectStandardError = true;
            var p = Process.Start(psi);
            string so = p.StandardOutput.ReadToEnd();
            string se = p.StandardError.ReadToEnd();
            p.WaitForExit();
            if (so.Trim() != "") Log(so.Trim());
            if (se.Trim() != "") Log(se.Trim());
            if (p.ExitCode != 0 || !File.Exists(output)) throw new Exception("Compile failed (exit " + p.ExitCode + ").");

            bar.Value = 100;
            buildStatus.Text = "Done — " + (new FileInfo(output).Length / 1024 / 1024) + " MB.";
            Log("DONE: " + output);
            built = true; builtOutput = output;
            step = 4; Render();
            MessageBox.Show("Setup.exe built:\n" + output, "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            buildStatus.Text = "Failed: " + ex.Message;
            Log("ERROR: " + ex.Message);
            MessageBox.Show("Build failed: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            try { if (work != null && Directory.Exists(work)) Directory.Delete(work, true); } catch { }
            building = false;
            backBtn.Enabled = true; nextBtn.Enabled = true;
            if (step == 3) nextBtn.Text = built ? "Finish →" : "Retry build";
        }
    }

    private void Log(string m)
    {
        logBox.AppendText("[" + DateTime.Now.ToString("HH:mm:ss") + "] " + m + "\r\n");
        logBox.SelectionStart = logBox.Text.Length;
        logBox.ScrollToCaret();
        Application.DoEvents();
    }

    private static string FindCsc()
    {
        var candidates = new List<string>();
        string win = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        foreach (var baseDir in new[] { Path.Combine(win, "Microsoft.NET", "Framework64"), Path.Combine(win, "Microsoft.NET", "Framework") })
        {
            try
            {
                if (!Directory.Exists(baseDir)) continue;
                var dirs = Directory.GetDirectories(baseDir, "v*");
                Array.Sort(dirs);
                Array.Reverse(dirs);
                foreach (var d in dirs) candidates.Add(Path.Combine(d, "csc.exe"));
            }
            catch { }
        }
        foreach (var c in candidates) if (File.Exists(c)) return c;
        return null;
    }

    private static string AdminManifest(string appName)
    {
        string safe = appName.Replace(" ", "").Replace("\"", "");
        return "<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n<assembly xmlns=\"urn:schemas-microsoft-com:asm.v1\" manifestVersion=\"1.0\">\r\n"
            + "  <assemblyIdentity version=\"1.0.0.0\" name=\"" + safe + ".Setup\" type=\"win32\" />\r\n"
            + "  <trustInfo xmlns=\"urn:schemas-microsoft-com:asm.v3\"><security><requestedPrivileges xmlns=\"urn:schemas-microsoft-com:asm.v3\">"
            + "<requestedExecutionLevel level=\"requireAdministrator\" uiAccess=\"false\" /></requestedPrivileges></security></trustInfo>\r\n</assembly>";
    }

    private static string SetupTemplate(string appName, string version, string publisher, string mainExe, bool desktopShortcut)
    {
        string sApp = appName.Replace("\"", "");
        string sVer = version.Replace("\"", "");
        string sPub = publisher.Replace("\"", "");
        string sExe = mainExe.Replace("\"", "");
        string regId = "";
        foreach (char c in sApp) if (char.IsLetterOrDigit(c)) regId += c;
        if (regId == "") regId = "App";
        string desktopCode = desktopShortcut
            ? "                CreateShortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), AppName + \".lnk\"), Path.Combine(target, MainExe), target);\r\n"
            : "";
        var sb = new StringBuilder();
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using System.Diagnostics;");
        sb.AppendLine("using System.IO;");
        sb.AppendLine("using System.IO.Compression;");
        sb.AppendLine("using System.Reflection;");
        sb.AppendLine("using System.Windows.Forms;");
        sb.AppendLine("using Microsoft.Win32;");
        sb.AppendLine("public class SetupForm : Form");
        sb.AppendLine("{");
        sb.AppendLine("    private const string AppName = \"" + sApp + "\";");
        sb.AppendLine("    private const string AppVersion = \"" + sVer + "\";");
        sb.AppendLine("    private const string Publisher = \"" + sPub + "\";");
        sb.AppendLine("    private const string MainExe = \"" + sExe + "\";");
        sb.AppendLine("    private const string UninstallKey = @\"SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\" + regId + "\";");
        sb.AppendLine("    private TextBox pathBox; private Button browseBtn, installBtn; private ProgressBar bar; private Label status; private Label step;");
        sb.AppendLine("    public SetupForm()");
        sb.AppendLine("    {");
        sb.AppendLine("        Text = AppName + \" Setup \" + AppVersion; Width = 500; Height = 340;");
        sb.AppendLine("        StartPosition = FormStartPosition.CenterScreen; FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false;");
        sb.AppendLine("        var header = new Label() { Text = AppName + \" \" + AppVersion, Left = 20, Top = 12, Width = 440, Font = new System.Drawing.Font(\"Segoe UI\", 14F, System.Drawing.FontStyle.Bold) };");
        sb.AppendLine("        var sub = new Label() { Text = Publisher + \"  •  choose where to install\", Left = 20, Top = 40, Width = 440, ForeColor = System.Drawing.Color.Gray };");
        sb.AppendLine("        var lbl = new Label() { Text = \"Installation folder:\", Left = 20, Top = 72, Width = 440 };");
        sb.AppendLine("        pathBox = new TextBox() { Left = 20, Top = 94, Width = 340 };");
        sb.AppendLine("        pathBox.Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), AppName);");
        sb.AppendLine("        browseBtn = new Button() { Text = \"Browse...\", Left = 368, Top = 93, Width = 94, Height = 26 };");
        sb.AppendLine("        browseBtn.Click += (s, e) => { using (var d = new FolderBrowserDialog()) { d.Description = \"Choose installation folder\"; d.SelectedPath = pathBox.Text; if (d.ShowDialog() == DialogResult.OK) pathBox.Text = Path.Combine(d.SelectedPath, AppName); } };");
        sb.AppendLine("        step = new Label() { Text = \"Ready to install.\", Left = 20, Top = 132, Width = 440, Font = new System.Drawing.Font(\"Segoe UI\", 9F, System.Drawing.FontStyle.Bold) };");
        sb.AppendLine("        bar = new ProgressBar() { Left = 20, Top = 156, Width = 442, Height = 16 };");
        sb.AppendLine("        status = new Label() { Text = \"Click Install to continue.\", Left = 20, Top = 180, Width = 440, ForeColor = System.Drawing.Color.Gray };");
        sb.AppendLine("        installBtn = new Button() { Text = \"Install\", Left = 342, Top = 218, Width = 120, Height = 38 };");
        sb.AppendLine("        installBtn.Click += (s, e) => RunInstall();");
        sb.AppendLine("        Controls.Add(header); Controls.Add(sub); Controls.Add(lbl); Controls.Add(pathBox); Controls.Add(browseBtn); Controls.Add(step); Controls.Add(bar); Controls.Add(status); Controls.Add(installBtn);");
        sb.AppendLine("    }");
        sb.AppendLine("    private void RunInstall()");
        sb.AppendLine("    {");
        sb.AppendLine("        string target = pathBox.Text.Trim();");
        sb.AppendLine("        if (target == \"\") { MessageBox.Show(\"Please choose an installation folder.\"); return; }");
        sb.AppendLine("        installBtn.Enabled = false; browseBtn.Enabled = false;");
        sb.AppendLine("        try");
        sb.AppendLine("        {");
        sb.AppendLine("            step.Text = \"Installing...\"; status.Text = \"Installing to \" + target + \" ...\"; bar.Value = 10; Application.DoEvents();");
        sb.AppendLine("            var asm = Assembly.GetExecutingAssembly();");
        sb.AppendLine("            bool isUpgrade = false;");
        sb.AppendLine("            string installedVer = \"\";");
        sb.AppendLine("            try { using (var k = Registry.LocalMachine.OpenSubKey(UninstallKey)) { if (k != null) { isUpgrade = true; installedVer = k.GetValue(\"DisplayVersion\") as string ?? \"\"; } } } catch { }");
        sb.AppendLine("            if (isUpgrade)");
        sb.AppendLine("            {");
        sb.AppendLine("                int cmp = CompareVersions(installedVer, AppVersion);");
        sb.AppendLine("                if (cmp > 0) { MessageBox.Show(\"A NEWER version (\" + installedVer + \") is already installed. This setup (\" + AppVersion + \") will not downgrade it.\"); installBtn.Enabled = true; browseBtn.Enabled = true; step.Text = \"Blocked.\"; return; }");
        sb.AppendLine("                if (cmp == 0) { var r0 = MessageBox.Show(\"Version \" + AppVersion + \" is already installed. Reinstall it?\", \"Already installed\", MessageBoxButtons.YesNo, MessageBoxIcon.Question); if (r0 != DialogResult.Yes) { installBtn.Enabled = true; browseBtn.Enabled = true; return; } }");
        sb.AppendLine("                else { var q = MessageBox.Show(\"Version \" + installedVer + \" is installed.\\n\\nYes = upgrade in place to \" + AppVersion + \"\\nNo = fresh install (wipes the folder first)\\nCancel = stop\", \"Existing installation found\", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);");
        sb.AppendLine("                if (q == DialogResult.Cancel) { installBtn.Enabled = true; browseBtn.Enabled = true; return; }");
        sb.AppendLine("                if (q == DialogResult.No) { try { Directory.Delete(target, true); } catch (Exception ex) { throw new Exception(\"Cannot clear old folder: \" + ex.Message); } } }");
        sb.AppendLine("            }");
        sb.AppendLine("            using (var rs = asm.GetManifestResourceStream(\"app.zip\"))");
        sb.AppendLine("            {");
        sb.AppendLine("                if (rs == null) throw new Exception(\"Embedded payload missing.\");");
        sb.AppendLine("                string tmp = Path.Combine(Path.GetTempPath(), \"app_payload.zip\");");
        sb.AppendLine("                using (var fs = File.Create(tmp)) rs.CopyTo(fs);");
        sb.AppendLine("                bar.Value = 40; step.Text = \"Extracting files...\"; Application.DoEvents();");
        sb.AppendLine("                Directory.CreateDirectory(target);");
        sb.AppendLine("                using (var zip = ZipFile.OpenRead(tmp))");
        sb.AppendLine("                {");
        sb.AppendLine("                    var wanted = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);");
        sb.AppendLine("                    foreach (var entry in zip.Entries)");
        sb.AppendLine("                    {");
        sb.AppendLine("                        string clean = entry.FullName.Replace('/', Path.DirectorySeparatorChar);");
        sb.AppendLine("                        if (clean == \"\" || clean == \".\" || clean.StartsWith(\"..\" + Path.DirectorySeparatorChar) || clean.Equals(\"..\")) continue;");
        sb.AppendLine("                        string dest = Path.GetFullPath(Path.Combine(target, clean));");
        sb.AppendLine("                        string root = Path.GetFullPath(target);");
        sb.AppendLine("                        if (!dest.StartsWith(root + Path.DirectorySeparatorChar) && !dest.Equals(root)) continue;");
        sb.AppendLine("                        wanted.Add(dest);");
        sb.AppendLine("                        if (entry.FullName.EndsWith(\"/\") || entry.FullName.EndsWith(\"\\\\\") || entry.Name == \"\") { Directory.CreateDirectory(dest); continue; }");
        sb.AppendLine("                        Directory.CreateDirectory(Path.GetDirectoryName(dest));");
        sb.AppendLine("                        if (dest.EndsWith(\"uninstall.exe\", System.StringComparison.OrdinalIgnoreCase)) continue;");
        sb.AppendLine("                        try { entry.ExtractToFile(dest, true); }");
        sb.AppendLine("                        catch (IOException) { throw new Exception(\"Cannot replace '\" + entry.FullName + \"' - close the running app and retry.\"); }");
        sb.AppendLine("                    }");
        sb.AppendLine("                    step.Text = \"Cleaning up old files...\"; Application.DoEvents();");
        sb.AppendLine("                    foreach (var f in Directory.GetFiles(target, \"*\", SearchOption.AllDirectories))");
        sb.AppendLine("                    {");
        sb.AppendLine("                        if (f.EndsWith(\"uninstall.exe\", System.StringComparison.OrdinalIgnoreCase)) continue;");
        sb.AppendLine("                        if (!wanted.Contains(Path.GetFullPath(f))) { try { File.Delete(f); } catch { } }");
        sb.AppendLine("                    }");
        sb.AppendLine("                }");
        sb.AppendLine("                try { File.Delete(tmp); } catch { }");
        sb.AppendLine("            }");
        sb.AppendLine("            bar.Value = 65; step.Text = \"Creating shortcuts...\"; Application.DoEvents();");
        sb.AppendLine("            string self = asm.Location;");
        sb.AppendLine("            try { File.Copy(self, Path.Combine(target, \"uninstall.exe\"), true); } catch { }");
        sb.AppendLine("            bar.Value = 75; Application.DoEvents();");
        sb.AppendLine("            try");
        sb.AppendLine("            {");
        sb.AppendLine("                string linkDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), AppName);");
        sb.AppendLine("                Directory.CreateDirectory(linkDir);");
        sb.AppendLine("                CreateShortcut(Path.Combine(linkDir, AppName + \".lnk\"), Path.Combine(target, MainExe), target);");
        sb.AppendLine("                CreateShortcut(Path.Combine(linkDir, \"Uninstall \" + AppName + \".lnk\"), Path.Combine(target, \"uninstall.exe\"), target);");
        sb.Append(desktopCode);
        sb.AppendLine("            }");
        sb.AppendLine("            catch { }");
        sb.AppendLine("            bar.Value = 88; step.Text = \"Registering...\"; Application.DoEvents();");
        sb.AppendLine("            try");
        sb.AppendLine("            {");
        sb.AppendLine("                using (var key = Registry.LocalMachine.CreateSubKey(UninstallKey))");
        sb.AppendLine("                {");
        sb.AppendLine("                    key.SetValue(\"DisplayName\", AppName); key.SetValue(\"DisplayVersion\", AppVersion);");
        sb.AppendLine("                    key.SetValue(\"Publisher\", Publisher); key.SetValue(\"InstallLocation\", target);");
        sb.AppendLine("                    key.SetValue(\"DisplayIcon\", Path.Combine(target, MainExe));");
        sb.AppendLine("                    key.SetValue(\"UninstallString\", \"\\\"\" + Path.Combine(target, \"uninstall.exe\") + \"\\\" /uninstall\");");
        sb.AppendLine("                    try { long bytes = 0; foreach (var f in new DirectoryInfo(target).GetFiles(\"*\", SearchOption.AllDirectories)) bytes += f.Length; key.SetValue(\"EstimatedSize\", (int)(bytes / 1024), RegistryValueKind.DWord); } catch { }");
        sb.AppendLine("                    try { key.SetValue(\"InstallDate\", DateTime.Now.ToString(\"yyyyMMdd\")); } catch { }");
        sb.AppendLine("                    key.SetValue(\"NoModify\", 1); key.SetValue(\"NoRepair\", 1);");
        sb.AppendLine("                }");
        sb.AppendLine("            }");
        sb.AppendLine("            catch (Exception ex) { MessageBox.Show(\"Warning: could not register: \" + ex.Message); }");
        sb.AppendLine("            bar.Value = 100;");
        sb.AppendLine("            step.Text = isUpgrade ? \"Upgrade complete.\" : \"Install complete.\";");
        sb.AppendLine("            status.Text = isUpgrade ? (\"Upgraded to \" + AppVersion) : \"Installed successfully.\";");
        sb.AppendLine("            var r = MessageBox.Show((isUpgrade ? (\"Upgraded to \" + AppVersion) : \"Installed.\") + \" Launch now?\", \"Done\", MessageBoxButtons.YesNo, MessageBoxIcon.Information);");
        sb.AppendLine("            if (r == DialogResult.Yes) { try { Process.Start(Path.Combine(target, MainExe)); } catch { } }");
        sb.AppendLine("            Close();");
        sb.AppendLine("        }");
        sb.AppendLine("        catch (Exception ex) { step.Text = \"Failed.\"; status.Text = \"Failed: \" + ex.Message; MessageBox.Show(\"Install failed: \" + ex.Message); installBtn.Enabled = true; browseBtn.Enabled = true; }");
        sb.AppendLine("    }");
        sb.AppendLine("    private static int CompareVersions(string a, string b)");
        sb.AppendLine("    {");
        sb.AppendLine("        try");
        sb.AppendLine("        {");
        sb.AppendLine("            var pa = (a ?? \"\").Split('.'); var pb = (b ?? \"\").Split('.');");
        sb.AppendLine("            int n = System.Math.Max(pa.Length, pb.Length);");
        sb.AppendLine("            for (int i = 0; i < n; i++)");
        sb.AppendLine("            {");
        sb.AppendLine("                int x = 0, y = 0;");
        sb.AppendLine("                if (i < pa.Length) int.TryParse(pa[i], out x);");
        sb.AppendLine("                if (i < pb.Length) int.TryParse(pb[i], out y);");
        sb.AppendLine("                if (x != y) return x.CompareTo(y);");
        sb.AppendLine("            }");
        sb.AppendLine("            return 0;");
        sb.AppendLine("        }");
        sb.AppendLine("        catch { return 0; }");
        sb.AppendLine("    }");
        sb.AppendLine("    private static void CreateShortcut(string lnk, string exe, string workDir)");
        sb.AppendLine("    {");
        sb.AppendLine("        dynamic shell = Activator.CreateInstance(Type.GetTypeFromProgID(\"WScript.Shell\"));");
        sb.AppendLine("        dynamic s = shell.CreateShortcut(lnk); s.TargetPath = exe; s.WorkingDirectory = workDir; s.Save();");
        sb.AppendLine("    }");
        sb.AppendLine("    [STAThread]");
        sb.AppendLine("    public static void Main(string[] args)");
        sb.AppendLine("    {");
        sb.AppendLine("        foreach (var a in args) if (a.Equals(\"/uninstall\", StringComparison.OrdinalIgnoreCase)) { RunUninstall(args); return; }");
        sb.AppendLine("        Application.EnableVisualStyles(); Application.Run(new SetupForm());");
        sb.AppendLine("    }");
        sb.AppendLine("    private static void RunUninstall(string[] args)");
        sb.AppendLine("    {");
        sb.AppendLine("        bool quiet = false;");
        sb.AppendLine("        foreach (var a in args) if (a.Equals(\"/quiet\", StringComparison.OrdinalIgnoreCase)) quiet = true;");
        sb.AppendLine("        string target = null;");
        sb.AppendLine("        try { using (var key = Registry.LocalMachine.OpenSubKey(UninstallKey)) { if (key != null) target = key.GetValue(\"InstallLocation\") as string; } } catch { }");
        sb.AppendLine("        if (string.IsNullOrEmpty(target)) target = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), AppName);");
        sb.AppendLine("        if (!quiet) { var r = MessageBox.Show(\"Uninstall \" + AppName + \" from\\n\" + target + \"?\", \"Uninstall\", MessageBoxButtons.YesNo, MessageBoxIcon.Question); if (r != DialogResult.Yes) return; }");
        sb.AppendLine("        try { string linkDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), AppName); if (Directory.Exists(linkDir)) Directory.Delete(linkDir, true); string desk = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), AppName + \".lnk\"); if (File.Exists(desk)) File.Delete(desk); } catch { }");
        sb.AppendLine("        try { Registry.LocalMachine.DeleteSubKey(UninstallKey, false); } catch { }");
        sb.AppendLine("        try { var psi = new ProcessStartInfo(\"cmd.exe\", \"/c ping -n 3 127.0.0.1 >nul & rmdir /s /q \\\"\" + target + \"\\\"\"); psi.CreateNoWindow = true; psi.WindowStyle = ProcessWindowStyle.Hidden; Process.Start(psi); } catch (Exception ex) { if (!quiet) MessageBox.Show(\"Could not remove files: \" + ex.Message); return; }");
        sb.AppendLine("        if (!quiet) MessageBox.Show(AppName + \" has been uninstalled.\", \"Uninstalled\", MessageBoxButtons.OK, MessageBoxIcon.Information);");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    [STAThread]
    public static void Main()
    {
        Application.EnableVisualStyles();
        Application.Run(new WizardForm());
    }
}
