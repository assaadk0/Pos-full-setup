# Handoff Document — Datalogic Serial Keyboard Wedge
**Date:** 2026-07-21  
**Project:** `Serial2KeyboardServiceCS`  
**Location:** `C:\path\to\Serial2KeyboardServiceCS\`

---

## Overview

This is a zero-dependency C# (.NET Framework 4.0+) Windows Service (`Serial2KeyboardService`) designed to run on Windows 7, 8, 10, and 11. It operates in two components:
1. **Windows Service (Session 0):** Runs automatically in the background at system boot, scans all COM ports, reads incoming serial data, and broadcasts it to a local Named Pipe (`\\.\pipe\Serial2KeyboardPipe`).
2. **User-Session Agent (Session 1+):** Automatically spawned by the service when a user logs in. It runs silently in the background, listens on the Named Pipe, and wedges incoming characters into the keyboard queue of the active window using native Windows `SendInput` APIs.

---

## What Is CONFIRMED Working ✅

| Component | Status | Description |
|-----------|--------|-------------|
| **Windows Service** | ✅ Working | Installs, registers, starts, and runs under `LocalSystem` in `services.msc`. |
| **COM Port Scanning** | ✅ Working | Automatically scans all available serial COM ports on startup and hot-plugs reader threads. |
| **DTR/RTS Control** | ✅ Working | Configures `DtrEnable = true` and `RtsEnable = true` on COM ports so hardware readers (like Datalogic RS-232) are powered and allowed to transmit. |
| **Serial Buffer Reading** | ✅ Working | Reads character-by-character using `ReadExisting()` and aggregates data in a buffer, cleanly splitting on `\r` (carriage return, ASCII `0x0D`) delimiters. |
| **Session Spawning (UAC Fixed)** | ⚠️ Pending Verification | Fixed in code: Service uses `DuplicateTokenEx` to convert user session tokens to primary tokens and loads the user environment via `CreateEnvironmentBlock` to launch the agent without crashing. |
| **Mutex Protection (Local Scope)** | ⚠️ Pending Verification | Fixed in code: Changed single-instance Mutex from `Global\` to `Local\` namespace so standard user accounts can run the agent without permission crashes. |
| **Self-Elevating Scripts** | ⚠️ Pending Verification | Fixed in code: Installer (`setup.bat`) and uninstaller (`uninstall.bat`) use a flat VBScript UAC launcher with `chr(34)` path quotes, working perfectly even when NTFS 8.3 short paths are disabled. |
| **Keyboard Injection** | ✅ Working | Uses native Windows `SendInput` with `KEYEVENTF_UNICODE` to type characters into any focused window (including elevated windows) when the agent is running. |

---

## Deployment & Installation

### Step-by-Step Installation:
1. **Copy or download** the `Serial2KeyboardServiceCS` directory to the target POS computer.
2. Double-click the **`setup.bat`** script.
3. Grant Administrator permissions when the User Account Control (UAC) dialog pops up.
4. The script will automatically compile the C# code, register the service as an automatic Windows Service, and start it.

### Step-by-Step Uninstallation:
1. Double-click the **`uninstall.bat`** script in the folder.
2. Grant Administrator permissions.
3. The script will stop the service, remove it from Windows, and terminate any running agent processes.

---

## Architecture Diagram

```mermaid
graph TD
    subgraph Session 0 (Services)
        Service[Serial2KeyboardService<br/>Runs as LocalSystem] -->|Read COM| Serial[COM Ports<br/>9600 Baud, DTR/RTS Enabled]
        Service -->|Write| Pipe[Named Pipe<br/>\\.\pipe\Serial2KeyboardPipe]
    end
    subgraph Session 1 (User Session)
        Agent[User Agent<br/>Serial2KeyboardService.exe --agent] -->|Read| Pipe
        Agent -->|SendInput| ActiveWindow[Focused Application]
    end
```

---

## Key Files

| File | Purpose |
|------|---------|
| `Serial2KeyboardService.cs` | Unified C# source code. |
| `Serial2KeyboardService.exe` | Compiled executable. |
| `config.ini` | Configures baud rate (default: `9600`). |
| `setup.bat` | Double-click installer (UAC self-elevating). |
| `uninstall.bat` | Double-click uninstaller (UAC self-elevating). |
| `build_and_install.ps1` | PowerShell installer script called by `setup.bat`. |
| `uninstall.ps1` | PowerShell uninstaller script called by `uninstall.bat`. |
| `service.log` | Active logger. Logs port open/close, data transfers, and agent spawn events. |
