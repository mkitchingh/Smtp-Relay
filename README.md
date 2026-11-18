# Windows SMTP Relay Service
Microsoft removed the built in SMTP relay server from Windows. This is a replacement.

Every other option I could find was no longer developed, overkill, or lacking features unless you used the paid version. 

It is actively being developed and I will always try to keep it up to to date with current technologies.

Config GUI shown below. All options are pretty basic and self explanatory. Service will continue to run when the GUI is closed.
GUI will launch after install. It may take a few seconds while Windows scans it for the first time. Start menu shortcut is named SMTP Relay Config.

This app does not phone home. No telemetry, no update checks, no external connections of any kind beyond relaying your mail.

Current release: [https://github.com/mkitchingh/Smtp-Relay/releases/download/v1.5.5/SmtpRelaySetup.exe](https://github.com/mkitchingh/Smtp-Relay/releases/download/v1.5.5/SmtpRelaySetup.exe)

**You may see Windows Defender or Unknown Publisher messages when you install. I'd prefer not to pay the code signing certificate cost, but I will if it causes a problem for people.

## Silent / Unattended Installation

The installer supports a `/config=` parameter for deploying with a pre-built configuration file. This is useful for distributing the app to multiple machines without requiring users to configure it manually.

```
SmtpRelaySetup.exe /config="C:\path\to\config.json"
```

When `/config=` is supplied:
- The config file is copied into the install directory before the service starts, always overwriting any existing config.
- The "Open SMTP Relay Config" post-install step is skipped automatically since the service is already configured.

You can combine this with Inno Setup's standard silent install flag:

```
SmtpRelaySetup.exe /silent /config="C:\path\to\config.json"
```

The config file is standard JSON. The easiest way to generate one is to configure a machine using the GUI and then copy the `config.json` from `C:\Program Files\SMTP Relay\`.

<img width="908" height="793" alt="SMTP-Pic153" src="https://github.com/user-attachments/assets/19c7e93c-6edf-43b4-bf47-c89e670de76c" />
