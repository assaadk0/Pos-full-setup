# Serial to Keyboard Windows Service (C#)

This directory contains a zero-dependency C# solution that runs as a native Windows Service (Session 0) and automatically launches a typing agent inside the interactive user's session (Session 1+). 

This solution is fully compatible with **Windows 7, 8, 10, and 11** out of the box.

---

## File Structure

*   `Serial2KeyboardService.cs` — The unified C# source code containing both the Windows Service and the User Session Agent.
*   `Serial2KeyboardService.exe` — The compiled executable.
*   `config.ini` — Configuration file to customize the COM port baud rate (defaults to 9600).
*   `build_and_install.ps1` — PowerShell script to compile the source code and install it as an automatic Windows service.
*   `uninstall.ps1` — PowerShell script to cleanly stop and delete the service.
*   `setup.bat` — Easiest method: double-click this file to request Admin privileges and automatically install the service.
*   `uninstall.bat` — Double-click this file to request Admin privileges and automatically stop and remove the service.

---

## Installation Steps

There are two ways to install the service on a POS terminal:

### Method A: Double-Click Installer (Recommended)
1. Navigate to the folder in File Explorer.
2. Double-click the **`setup.bat`** file.
3. Windows will prompt a User Account Control (UAC) dialog. Click **Yes** to grant administrator privileges.
4. A console window will open, automatically compile the C# file, and install/start the Windows Service.
5. Once complete, press any key to close the console window.

### Method B: Manual PowerShell Installation (Alternative)
1. Open **PowerShell as Administrator** (search for PowerShell in Start, right-click, and select "Run as Administrator").
2. Navigate to the directory where you extracted/downloaded the files:
   ```powershell
   cd "C:\path\to\Serial2KeyboardServiceCS"
   ```
3. Execute the installation script:
   ```powershell
   powershell -ExecutionPolicy Bypass -File .\build_and_install.ps1
   ```

**What the installation process does:**
1. Calls the built-in Windows C# compiler (`csc.exe`) to compile `Serial2KeyboardService.cs` into `Serial2KeyboardService.exe`.
2. Registers `Serial2KeyboardService.exe` as a native Windows service named `Serial2KeyboardService`.
3. Configures the service startup type to **Automatic** (so it starts on boot).
4. Starts the service.

---

## Verification & Monitoring

Once installed, you can verify that everything is running correctly:

1.  **Check the Service Status:**
    Open **Services** (`services.msc`) and look for **Serial to Keyboard Service**. It should show **Running** and **Automatic** startup.
    
    Or run this in PowerShell:
    ```powershell
    Get-Service Serial2KeyboardService
    ```

2.  **Verify the User Session Agent:**
    Open **Task Manager** (`taskmgr.exe`), go to the **Details** tab, and verify that `Serial2KeyboardService.exe` is running as the logged-in user with the command-line argument `--agent`.
    *(The service will automatically relaunch this process if it is ever closed or crashed).*

3.  **Inspect the Logs:**
    A file named `service.log` will be created in the same directory. It records service actions, including COM port detection, client connections, and data transfer events:
    ```text
    2026-07-21 12:00:00 - Service is starting...
    2026-07-21 12:00:00 - Named Pipe Server waiting for connection...
    2026-07-21 12:00:00 - Serial Monitor loop started.
    2026-07-21 12:00:00 - New serial port detected: COM3. Launching reader.
    2026-07-21 12:00:01 - User token active for session 1. Spawning user-session agent...
    2026-07-21 12:00:01 - Named Pipe Client connected.
    ```

---

## Uninstallation

To cleanly stop the service and delete it from the system:

### Method A: Double-Click Uninstaller (Recommended)
1. Navigate to the folder in File Explorer.
2. Double-click the **`uninstall.bat`** file.
3. Windows will prompt a User Account Control (UAC) dialog. Click **Yes** to grant administrator privileges.
4. Once complete, press any key to close the window.

### Method B: Manual PowerShell Uninstallation (Alternative)
1. Open **PowerShell as Administrator**.
2. Navigate to the folder and run:
    ```powershell
    powershell -ExecutionPolicy Bypass -File .\uninstall.ps1
    ```

**What the uninstallation process does:**
1. Stops the background service.
2. Deletes the service registration from the Windows Service Control Manager.
3. Terminates any active `Serial2KeyboardService.exe` processes running in the user session.
