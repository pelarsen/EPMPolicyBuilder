const fs = require("fs");
const {
  Document, Packer, Paragraph, TextRun, Table, TableRow, TableCell,
  Header, Footer, AlignmentType, LevelFormat, ExternalHyperlink,
  TabStopType, TabStopPosition,
  TableOfContents, HeadingLevel, BorderStyle, WidthType, ShadingType,
  PageNumber, PageBreak
} = require("docx");

const ACCENT = "2E75B6";
const DARK = "222222";
const GREY = "595959";

const border = { style: BorderStyle.SINGLE, size: 2, color: "CCCCCC" };
const cellBorders = { top: border, left: border, bottom: border, right: border };

function h1(text) {
  return new Paragraph({ heading: HeadingLevel.HEADING_1, children: [new TextRun(text)] });
}
function h2(text) {
  return new Paragraph({ heading: HeadingLevel.HEADING_2, children: [new TextRun(text)] });
}
function h3(text) {
  return new Paragraph({ heading: HeadingLevel.HEADING_3, children: [new TextRun(text)] });
}
function p(text, opts = {}) {
  return new Paragraph({ spacing: { after: 160 }, children: [new TextRun({ text, ...opts })] });
}
function bullet(text, opts = {}) {
  return new Paragraph({
    numbering: { reference: "bullets", level: 0 },
    spacing: { after: 80 },
    children: [new TextRun({ text, ...opts })]
  });
}
function numbered(text, opts = {}) {
  return new Paragraph({
    numbering: { reference: "numbers", level: 0 },
    spacing: { after: 80 },
    children: [new TextRun({ text, ...opts })]
  });
}
function code(text) {
  // NOTE: docx-js hardcodes w:pBdr child order as top,bottom,left,right
  // (ignoring JS key order), which violates the OOXML schema
  // (top,left,bottom,right required). After generating the .docx, run
  // the post-process step at the bottom of this file (or the fix-pbdr
  // block) to reorder <w:pBdr> children before validating/shipping.
  return new Paragraph({
    shading: { fill: "F3F3F3", type: ShadingType.CLEAR },
    spacing: { after: 160, before: 80 },
    border: { top: border, left: border, bottom: border, right: border },
    children: [new TextRun({ text, font: "Consolas", size: 19 })]
  });
}
function note(title, text) {
  return new Paragraph({
    shading: { fill: "EAF1FB", type: ShadingType.CLEAR },
    border: { left: { style: BorderStyle.SINGLE, size: 24, color: ACCENT } },
    spacing: { before: 120, after: 160 },
    indent: { left: 200 },
    children: [
      new TextRun({ text: title + ": ", bold: true, color: ACCENT }),
      new TextRun({ text })
    ]
  });
}

function makeTable(headerRow, rows, colWidths) {
  const total = colWidths.reduce((a, b) => a + b, 0);
  const headerCells = headerRow.map((text, i) => new TableCell({
    borders: cellBorders,
    width: { size: colWidths[i], type: WidthType.DXA },
    shading: { fill: "D9E2F3", type: ShadingType.CLEAR },
    margins: { top: 80, bottom: 80, left: 120, right: 120 },
    children: [new Paragraph({ children: [new TextRun({ text, bold: true })] })]
  }));
  const bodyRows = rows.map(cols => new TableRow({
    children: cols.map((text, i) => new TableCell({
      borders: cellBorders,
      width: { size: colWidths[i], type: WidthType.DXA },
      margins: { top: 80, bottom: 80, left: 120, right: 120 },
      children: [new Paragraph({ children: [new TextRun({ text })] })]
    }))
  }));
  return new Table({
    width: { size: total, type: WidthType.DXA },
    columnWidths: colWidths,
    rows: [new TableRow({ children: headerCells, tableHeader: true }), ...bodyRows]
  });
}

const doc = new Document({
  styles: {
    default: { document: { run: { font: "Arial", size: 22, color: DARK } } },
    paragraphStyles: [
      { id: "Heading1", name: "Heading 1", basedOn: "Normal", next: "Normal", quickFormat: true,
        run: { size: 32, bold: true, font: "Arial", color: ACCENT },
        paragraph: { spacing: { before: 360, after: 200 }, outlineLevel: 0, pageBreakBefore: true } },
      { id: "Heading2", name: "Heading 2", basedOn: "Normal", next: "Normal", quickFormat: true,
        run: { size: 26, bold: true, font: "Arial", color: DARK },
        paragraph: { spacing: { before: 280, after: 140 }, outlineLevel: 1 } },
      { id: "Heading3", name: "Heading 3", basedOn: "Normal", next: "Normal", quickFormat: true,
        run: { size: 23, bold: true, font: "Arial", color: DARK },
        paragraph: { spacing: { before: 200, after: 100 }, outlineLevel: 2 } },
    ]
  },
  numbering: {
    config: [
      { reference: "bullets",
        levels: [{ level: 0, format: LevelFormat.BULLET, text: "•", alignment: AlignmentType.LEFT,
          style: { paragraph: { indent: { left: 720, hanging: 360 } } } }] },
      { reference: "numbers",
        levels: [{ level: 0, format: LevelFormat.DECIMAL, text: "%1.", alignment: AlignmentType.LEFT,
          style: { paragraph: { indent: { left: 720, hanging: 360 } } } }] },
    ]
  },
  sections: [{
    properties: {
      page: {
        size: { width: 12240, height: 15840 },
        margin: { top: 1440, right: 1440, bottom: 1440, left: 1440 }
      }
    },
    headers: {
      default: new Header({
        children: [new Paragraph({
          tabStops: [{ type: TabStopType.RIGHT, position: TabStopPosition.MAX }],
          border: { bottom: { style: BorderStyle.SINGLE, size: 4, color: "CCCCCC", space: 4 } },
          children: [
            new TextRun({ text: "EPM Policy Builder", size: 16, color: GREY }),
            new TextRun({ text: "\tTechnical User Guide", size: 16, color: GREY })
          ]
        })]
      })
    },
    footers: {
      default: new Footer({
        children: [new Paragraph({
          alignment: AlignmentType.CENTER,
          children: [
            new TextRun({ text: "Page ", size: 16, color: GREY }),
            new TextRun({ children: [PageNumber.CURRENT], size: 16, color: GREY })
          ]
        })]
      })
    },
    children: [
      // ── Title page ─────────────────────────────────────────
      new Paragraph({ spacing: { before: 2400 }, alignment: AlignmentType.CENTER,
        children: [new TextRun({ text: "EPM Policy Builder", bold: true, size: 56, color: ACCENT })] }),
      new Paragraph({ alignment: AlignmentType.CENTER, spacing: { before: 200, after: 100 },
        children: [new TextRun({ text: "Technical User Guide", size: 32, color: DARK })] }),
      new Paragraph({ alignment: AlignmentType.CENTER, spacing: { before: 100, after: 2000 },
        children: [new TextRun({ text: "A WinUI 3 desktop tool for building and publishing Microsoft Intune Endpoint Privilege Management (EPM) elevation rules", size: 22, color: GREY })] }),
      new Paragraph({ alignment: AlignmentType.CENTER,
        children: [new TextRun({ text: "Version 1.0", size: 20, color: GREY })] }),
      new Paragraph({ alignment: AlignmentType.CENTER,
        children: [new TextRun({ text: new Date().toLocaleDateString("en-GB", { year: "numeric", month: "long", day: "numeric" }), size: 20, color: GREY })] }),

      new Paragraph({ children: [new PageBreak()] }),
      h1("Table of Contents"),
      new TableOfContents("Table of Contents", { hyperlink: true, headingStyleRange: "1-3" }),

      // ── 1. Overview ─────────────────────────────────────────
      h1("1. Overview"),
      p("EPM Policy Builder is a Windows desktop application (WinUI 3 / .NET) that streamlines the creation and deployment of Microsoft Intune Endpoint Privilege Management elevation rules. It lets IT administrators discover applications that need elevated permissions, inspect their file and signing metadata, configure elevation rules, and publish them directly to Intune via the Microsoft Graph API — without hand-authoring JSON or navigating the Intune admin center for every rule."),
      p("The application is organized as a single window with a left-hand navigation pane. Each item in the pane corresponds to a stage in the rule-building workflow:"),
      bullet("File Analysis — inspect a single executable, MSI, or PowerShell script."),
      bullet("Rule Builder — configure an elevation rule for the selected file."),
      bullet("Drive Scanner — bulk-scan folders for candidate applications."),
      bullet("Rule Suggestions — see which apps end users elevated without a rule in the last 30 days."),
      bullet("Certificates — manage reusable certificate settings used by publisher-based rules."),
      bullet("Policy Upload — review the generated rule JSON and publish it to an Intune EPM policy."),
      note("Scope", "This guide covers the application as implemented at the time of writing. Screens and Graph API behavior may evolve as Microsoft updates the Intune EPM feature set."),

      // ── 2. Prerequisites ────────────────────────────────────
      h1("2. Prerequisites"),
      h2("2.1 Client machine"),
      bullet("Windows 10 version 1809 (build 17763) or later, or Windows 11."),
      bullet("The correct installer package for the device architecture: x64 for Intel/AMD PCs, ARM64 for Windows on ARM devices."),
      bullet("If the installer is signed with a self-signed or internal certificate, the corresponding certificate must be trusted on the target machine before installation. Developer Mode can also be used for local testing, but it is not required when the signing certificate is already trusted."),
      h2("2.2 Microsoft Entra ID / Intune"),
      bullet("A Microsoft Intune license and the Endpoint Privilege Management add-on assigned in the tenant."),
      bullet("An Entra ID app registration used by EPM Policy Builder to call Microsoft Graph — either created automatically via the in-app Quick Setup wizard, or created manually beforehand (see section 4)."),
      bullet("An account with Global Administrator or Application Administrator rights to complete first-time setup (app registration + admin consent)."),
      bullet("Day-to-day users only need the Graph delegated permission DeviceManagementConfiguration.ReadWrite.All (and DeviceManagementManagedDevices.Read.All for Rule Suggestions) — they do not need admin rights once the app registration exists and consent has been granted."),

      // ── 3. Installing and running ───────────────────────────
      h1("3. Installing and Running the Application"),
      p("EPM Policy Builder is intended to be installed as an MSIX package. Use the installer that matches the target device architecture, then launch the app from the Start menu like any other Windows desktop application."),
      h2("3.1 Choose the correct installer"),
      numbered("Use the x64 installer on Intel or AMD-based Windows PCs: EPMPolicyBuilder_1.0.0.0_x64.msix."),
      numbered("Use the ARM64 installer on Windows on ARM devices: EPMPolicyBuilder_1.0.0.0_arm64.msix."),
      numbered("Distribute the MSIX file from your internal software portal, GitHub release, file share, or another trusted software distribution channel."),
      note("Architecture check", "If you are not sure which installer to use, open Settings > System > About and check System type. Most business laptops and desktops will use the x64 package."),
      h2("3.2 Install the MSIX package"),
      p("Before installation, make sure the signing certificate is already trusted on the device if the package was signed with an internal or self-signed certificate."),
      numbered("Download the correct .msix package to the target Windows device."),
      numbered("Double-click the .msix file and select Install in the App Installer window."),
      numbered("If your environment prefers command-line deployment, install the package from an elevated PowerShell session using:"),
      code("Add-AppxPackage .\\EPMPolicyBuilder_1.0.0.0_x64.msix"),
      p("For ARM64 devices, use the ARM64 package path in the same command."),
      note("Certificate trust", "If Windows reports that the publisher is not trusted, install the signing certificate into the local machine or current user trust store first, then run the installer again. In production, use a certificate that is already trusted across managed devices."),
      h2("3.3 Launch and begin using the app"),
      numbered("Open Start, search for EPM Policy Builder, and launch the app."),
      numbered("On first launch, open Settings from the navigation footer and complete Quick Setup or enter an existing Client ID and Tenant ID."),
      numbered("Use Connect to Intune to sign in with an account that has the required Intune and Graph access."),
      numbered("After connection succeeds, use File Analysis, Rule Builder, Drive Scanner, Rule Suggestions, Certificates, and Policy Upload as described in the walkthrough sections later in this guide."),
      note("Updates", "Installing a newer MSIX package with the same package identity upgrades the existing installation in place. End users do not need to uninstall the previous version first."),

      // ── 4. First-time setup ─────────────────────────────────
      h1("4. First-Time Setup"),
      p("Before any rules can be uploaded, the app needs an Entra ID app registration with permission to manage Intune device configuration. This is configured on the Settings page, reachable from the navigation pane footer (gear icon)."),
      h2("4.1 Quick Setup (recommended)"),
      p("The Settings page offers an automated path that avoids the Azure Portal entirely:"),
      numbered("Open Settings and select \u201cSign In and Create App Registration.\u201d"),
      numbered("Sign in with a Global Administrator or Application Administrator account in the browser window that opens."),
      numbered("The app calls Microsoft Graph on your behalf (using Microsoft's well-known Graph PowerShell public client as a bootstrap identity) to:"),
      bullet("Create a new Entra ID application registration named \u201cEPM Policy Builder.\u201d", { italics: true }),
      bullet("Create the corresponding service principal.", { italics: true }),
      bullet("Request the Microsoft Graph delegated permission for Endpoint Privilege Management (App ID 9241abd9-d0e6-425a-bd4f-47ba86e767a4).", { italics: true }),
      bullet("Grant tenant-wide admin consent for DeviceManagementConfiguration.ReadWrite.All and DeviceManagementManagedDevices.Read.All.", { italics: true }),
      numbered("On success, the new Client ID is displayed and saved automatically. Continue to the Certificates or Policy Upload page and sign in normally."),
      note("If admin consent fails", "The setup log will show a warning rather than fail outright. Grant consent manually afterwards in Azure Portal under App registrations > EPM Policy Builder > API permissions > Grant admin consent."),
      h2("4.2 Manual configuration"),
      p("If an app registration already exists (e.g., created by your security team), skip Quick Setup and fill in the Manual Configuration section instead:"),
      bullet("Application (Client) ID — the GUID of the existing app registration."),
      bullet("Directory (Tenant) ID — leave blank or set to \u201ccommon\u201d to allow sign-in from any tenant, or pin to a specific tenant GUID."),
      p("The app registration must have:"),
      bullet("The delegated Graph permission DeviceManagementConfiguration.ReadWrite.All (with admin consent granted)."),
      bullet("A public client redirect URI of https://login.microsoftonline.com/common/oauth2/nativeclient."),
      p("Click Save Settings to persist the values; they are stored locally and reused on subsequent launches."),

      // ── 5. Connecting to Intune ──────────────────────────────
      h1("5. Connecting to Microsoft Intune"),
      p("Once a Client ID is configured, use the \u201cConnect to Intune\u201d button — available in the navigation pane footer and again on the Policy Upload and Rule Suggestions pages — to sign in interactively. The app uses MSAL.NET with an interactive browser flow; a successful sign-in shows the connected user's name in the pane footer with a Disconnect option."),
      h2("5.1 Troubleshooting sign-in"),
      new Table({
        width: { size: 9360, type: WidthType.DXA },
        columnWidths: [3000, 6360],
        rows: [
          new TableRow({ children: [
            new TableCell({ borders: cellBorders, width: { size: 3000, type: WidthType.DXA }, shading: { fill: "D9E2F3", type: ShadingType.CLEAR }, margins: { top: 80, bottom: 80, left: 120, right: 120 }, children: [new Paragraph({ children: [new TextRun({ text: "Symptom", bold: true })] })] }),
            new TableCell({ borders: cellBorders, width: { size: 6360, type: WidthType.DXA }, shading: { fill: "D9E2F3", type: ShadingType.CLEAR }, margins: { top: 80, bottom: 80, left: 120, right: 120 }, children: [new Paragraph({ children: [new TextRun({ text: "Cause / Fix", bold: true })] })] }),
          ]}),
          new TableRow({ children: [
            new TableCell({ borders: cellBorders, width: { size: 3000, type: WidthType.DXA }, margins: { top: 80, bottom: 80, left: 120, right: 120 }, children: [new Paragraph({ children: [new TextRun("\u201cAzure AD App Not Configured\u201d warning")] })] }),
            new TableCell({ borders: cellBorders, width: { size: 6360, type: WidthType.DXA }, margins: { top: 80, bottom: 80, left: 120, right: 120 }, children: [new Paragraph({ children: [new TextRun("No Client ID has been saved yet. Use the \u201cGo to Settings\u201d shortcut to complete first-time setup (section 4).")] })] }),
          ]}),
          new TableRow({ children: [
            new TableCell({ borders: cellBorders, width: { size: 3000, type: WidthType.DXA }, margins: { top: 80, bottom: 80, left: 120, right: 120 }, children: [new Paragraph({ children: [new TextRun("AADSTS50011 redirect URI mismatch")] })] }),
            new TableCell({ borders: cellBorders, width: { size: 6360, type: WidthType.DXA }, margins: { top: 80, bottom: 80, left: 120, right: 120 }, children: [new Paragraph({ children: [new TextRun("The app registration is missing http://localhost as a redirect URI. Use \u201cFix Redirect URI Automatically\u201d on the Certificates page (requires signing in again as an admin), or add it manually in Azure Portal.")] })] }),
          ]}),
          new TableRow({ children: [
            new TableCell({ borders: cellBorders, width: { size: 3000, type: WidthType.DXA }, margins: { top: 80, bottom: 80, left: 120, right: 120 }, children: [new Paragraph({ children: [new TextRun("Sign-in window closes with no result")] })] }),
            new TableCell({ borders: cellBorders, width: { size: 6360, type: WidthType.DXA }, margins: { top: 80, bottom: 80, left: 120, right: 120 }, children: [new Paragraph({ children: [new TextRun("The user cancelled the browser sign-in. Retry the Connect action.")] })] }),
          ]}),
        ]
      }),
      new Paragraph({ text: "" }),

      // ── 6. Page-by-page walkthrough ─────────────────────────
      h1("6. Application Walkthrough"),

      h2("6.1 File Analysis"),
      p("Use this page to inspect a single .exe, .msi, or .ps1 file before building a rule for it."),
      numbered("Click Browse... and select the target file."),
      numbered("The app computes a SHA-256 hash, reads Win32 version resource fields (product name, internal name, file version, description, publisher), and checks the Authenticode signature."),
      numbered("Review the File Metadata card, then click \u201cUse This File in Rule Builder\u201d to carry the metadata into a new rule."),

      h2("6.2 Rule Builder"),
      p("Configures a single elevation rule (an ElevationRule object) before it is uploaded. If a file was analyzed or selected from the Scanner/Suggestions pages, its metadata is pre-populated in an information banner at the top."),
      h3("Rule Identity"),
      bullet("Rule Name — display name shown in the Intune EPM policy."),
      bullet("Description — free-text notes, optional."),
      h3("Elevation Settings"),
      bullet("Elevation Type — one of User Confirmed, Automatic, Deny, Support Approved, or Elevate As Current User."),
      bullet("Validation Options (shown for confirmation-based types) — require Business Justification and/or Windows Authentication before elevation is granted."),
      bullet("Child Process Behavior — Require Rule, Deny All, or Allow Elevated, controlling how spawned child processes are treated."),
      h3("Certificate / Signature"),
      bullet("Signature Source — Not Configured, a previously uploaded Reusable Certificate, or a new certificate file to upload."),
      bullet("If the analyzed file was code-signed, its certificate subject, thumbprint, and validity are auto-detected and shown in a confirmation banner."),
      p("Selecting \u201cBuild Rule & Go to Policy Upload\u201d validates the form, constructs the rule payload, and navigates to Policy Upload with the rule pre-loaded."),

      h2("6.3 Drive Scanner"),
      p("Bulk-discovers candidate applications instead of analyzing one file at a time."),
      numbered("Choose one or more Scan Locations (well-known folders such as Program Files) and/or browse to a Custom Folder."),
      numbered("Click Scan for Applications. The scan runs recursively and asynchronously, reporting progress and silently skipping folders the current user cannot read; Cancel stops an in-progress scan."),
      numbered("Filter, review, and multi-select results in the list (name, path, publisher, signed status, version)."),
      numbered("Either send the selection to Rule Builder for individual configuration, or use \u201cCreate Rules for Selected \u2192 Upload\u201d to batch-generate and upload rules for every selected file in one action."),

      h2("6.4 Rule Suggestions"),
      p("Surfaces real-world elevation activity from Intune: applications end users ran with elevated privileges in the last 30 days without a matching EPM rule. This helps prioritize which rules to author next instead of guessing."),
      numbered("Requires an active Intune connection; click Refresh Report to query the unmanaged elevations report."),
      numbered("Each row shows the application, publisher, version, and a 30-day elevation count badge."),
      numbered("Click \u201cCreate Rule \u2192\u201d on a row to jump straight into Rule Builder pre-populated with that application's details."),
      note("Data freshness", "The underlying Intune report refreshes roughly once every 24 hours, so newly elevated applications may take up to a day to appear."),

      h2("6.5 Certificates"),
      p("Manages the reusable certificate settings referenced by publisher-based elevation rules (Signature Source = Reusable Certificate in Rule Builder)."),
      bullet("Lists existing reusable certificate settings with subject, issuer, thumbprint, validity window, and an expiry badge (Valid, Expires Soon, or Expired)."),
      bullet("Upload New Certificate adds a .cer or .pfx file as a new reusable setting that can be selected from any future rule."),
      bullet("Refresh reloads the list from Intune."),
      bullet("This page also hosts the redirect URI auto-fix banner described in section 5.1."),

      h2("6.6 Policy Upload"),
      p("The final step: review the generated rule JSON and publish it to an Intune EPM policy."),
      numbered("If not already connected, sign in inline from this page."),
      numbered("Review the Rule JSON Preview — the exact payload that will be sent to Microsoft Graph."),
      numbered("Choose a Target Policy: either Add to Existing Policy (select from the dropdown, Refresh to reload the list) or Create New Policy (provide a name)."),
      numbered("Click Upload Rule to Intune. Progress and the resulting status (success or error) are shown in the InfoBar below the button."),
      numbered("If the upload fails, the exact request JSON sent to Graph is displayed in a red-bordered panel to aid troubleshooting."),
      note("Batch mode", "When rules are created from the Drive Scanner's batch action, this page switches to Batch Upload Mode and displays a summary banner instead of a single rule preview."),

      // ── 7. Typical workflows ────────────────────────────────
      h1("7. Typical Workflows"),
      h2("7.1 Ad hoc: one application at a time"),
      numbered("File Analysis \u2192 browse to the executable and review its metadata."),
      numbered("Rule Builder \u2192 configure elevation type, validation, and signature options."),
      numbered("Policy Upload \u2192 choose or create a target policy and upload."),
      h2("7.2 Bulk onboarding of an application estate"),
      numbered("Drive Scanner \u2192 scan Program Files and any custom application folders."),
      numbered("Filter and multi-select the applications that should be allowed to elevate."),
      numbered("Use \u201cCreate Rules for Selected \u2192 Upload\u201d to generate and publish rules in one batch."),
      h2("7.3 Data-driven prioritization"),
      numbered("Rule Suggestions \u2192 refresh the unmanaged elevations report."),
      numbered("Start with the applications with the highest 30-day elevation counts."),
      numbered("Click \u201cCreate Rule \u2192\u201d for each, refine in Rule Builder, and upload."),

      // ── 8. Permissions & security ────────────────────────────
      h1("8. Permissions and Security"),
      h2("8.1 Graph permissions used"),
      makeTable(
        ["Permission", "Type", "Used for"],
        [
          ["DeviceManagementConfiguration.ReadWrite.All", "Delegated", "Reading/creating EPM policies and elevation rules; day-to-day operation."],
          ["DeviceManagementManagedDevices.Read.All", "Delegated", "Reading the unmanaged elevations report for Rule Suggestions."],
          ["Application.ReadWrite.All", "Delegated (bootstrap only)", "One-time: creating the app registration during Quick Setup."],
          ["DelegatedPermissionGrant.ReadWrite.All", "Delegated (bootstrap only)", "One-time: granting admin consent during Quick Setup."],
        ],
        [4200, 2400, 2760]
      ),
      new Paragraph({ text: "" }),
      h2("8.2 Credential handling"),
      bullet("Authentication uses MSAL.NET with interactive, system-browser sign-in; no passwords are handled or stored by the app."),
      bullet("Client ID and Tenant ID are stored locally via the app's settings service; no client secret is used (public client / native app flow)."),
      bullet("Uploaded certificates (for publisher-based rules) are sent to Intune as reusable settings and are not retained locally after upload beyond the selected file path."),
      h2("8.3 Least privilege for day-to-day users"),
      p("Only the person performing first-time setup needs Global Administrator or Application Administrator rights. Once the app registration exists and consent has been granted, subsequent users need only be assigned the Intune role required to manage EPM policies (e.g., Endpoint Security Manager) — the app itself does not require elevated Entra ID roles to run."),

      // ── 9. Appendix ─────────────────────────────────────────
      h1("9. Appendix: Data Model Reference"),
      h2("9.1 ElevationRule"),
      makeTable(
        ["Field", "Description"],
        [
          ["RuleName / RuleDescription", "Display name and free-text description shown in Intune."],
          ["ElevationType", "UserConfirmed, Automatic, Deny, SupportApproved, or ElevateAsCurrentUser."],
          ["ChildProcessBehavior", "RequireRule, DenyAll, or AllowElevated."],
          ["SignatureSource", "NotConfigured, ReusableCertificate, or UploadCertificate."],
          ["ValidationBusinessJustification / ValidationWindowsAuthentication", "Additional confirmation requirements for confirmation-based elevation types."],
          ["FileMetadata", "The associated file's name, path, hash, version info, and signing details."],
        ],
        [3800, 5560]
      ),
      new Paragraph({ text: "" }),
      h2("9.2 ExeFileInfo (scan results)"),
      p("Captured for every executable found by the Drive Scanner or File Analysis page: file name and path, internal name, file/product description and version, company name, SHA-256 hash, and Authenticode signing status (signed flag, certificate subject, issuer, thumbprint, and serial number)."),
      h2("9.3 ReusableSetting"),
      p("Represents a certificate-based reusable setting stored in Intune: display name, description, certificate subject, issuer, thumbprint, and validity window, plus a computed expiry state (Valid, Expires Soon, or Expired) used to badge the Certificates page."),
      h2("9.4 ElevationSuggestion"),
      p("Represents one row of the unmanaged elevations report used by Rule Suggestions: application display name, file name, publisher, file version, and the count of elevations recorded over the last 30 days."),
    ]
  }]
});

Packer.toBuffer(doc).then(buffer => {
  fs.writeFileSync("EPM_Policy_Builder_Technical_User_Guide.docx", buffer);
  console.log("Document written.");
});
