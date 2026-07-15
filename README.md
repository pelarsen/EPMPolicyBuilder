# EPM Policy Builder

EPM Policy Builder is a Windows desktop app (WinUI 3 / .NET) for creating and publishing Microsoft Intune Endpoint Privilege Management (EPM) elevation rules.

It helps IT administrators move from manual JSON editing and repetitive portal clicks to a guided workflow that discovers applications, builds rules, and uploads them directly to Intune through Microsoft Graph.

## What The App Does

- Analyzes `.exe`, `.msi`, and `.ps1` files
- Collects file metadata and signing details (including SHA-256 hash and certificate info)
- Builds EPM elevation rules with validation and child-process behavior settings
- Scans drives/folders to find candidate apps in bulk
- Shows unmanaged elevation activity (Rule Suggestions) to prioritize rule creation
- Manages reusable certificate settings for publisher-based rule scenarios
- Uploads rule payloads directly to existing or new Intune EPM policies

## Main Workflow

1. Connect to Intune
2. Analyze or scan applications
3. Configure rule behavior in Rule Builder
4. Review generated JSON
5. Upload to a target EPM policy

## Requirements

- Windows 10 (1809+) or Windows 11
- Microsoft Intune with Endpoint Privilege Management add-on
- Entra app registration (created automatically via Quick Setup, or manually configured)
- Appropriate Graph permissions and admin consent for first-time setup

## Install

Use the MSIX package that matches your architecture:

- `EPMPolicyBuilder_1.0.0.0_x64.msix` for Intel/AMD systems
- `EPMPolicyBuilder_1.0.0.0_arm64.msix` for Windows on ARM

If your environment uses internal/self-signed certificates, trust the signing certificate before installation.

## Documentation

The full technical guide is included in the repository:

- `EPM_Policy_Builder_Technical_User_Guide.docx`

## Project Structure

- `Views/`, `ViewModels/`, `Models/`, `Services/`
- `Package.appxmanifest` for app package metadata
- `_gen_docs.js` for generating the technical guide

## Notes

This project is focused on practical EPM operations for enterprise admins and is designed to reduce rule-authoring time while improving consistency and auditability.
