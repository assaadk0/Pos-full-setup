# NCR 7197 Setup for Odoo POS on Windows

This guide configures an NCR 7197 receipt printer for Odoo POS through the Windows Odoo IoT service.

## 1. Install the Edge/Edgeport driver

Install the supplied Edge/Edgeport serial driver from the provided files before installing the NCR printer.

After installation:

1. Open **Device Manager**.
2. Expand **Ports (COM & LPT)**.
3. Find the new **Edge/EPIC Port**.
4. Write down its COM port number, for example:

```text
EPIC Port (COM7)
```

The NCR Windows printer queue must use this same COM port.

## 2. Install the NCR 7197 Windows driver

1. Install the NCR 7197 printer driver.
2. Open **Control Panel → Devices and Printers**.
3. Open **Printer properties** for the NCR 7197.
4. Open the **Ports** tab.
5. Select the same COM port created by the Edge/Edgeport driver.

Example:

```text
Edge/EPIC driver port: COM7
NCR 7197 Windows printer port: COM7
```

Do not assign the NCR printer to a different COM port.

## 3. Reset and configure the NCR 7197

Reset the printer before applying the final configuration.

### Enter configuration mode

1. Flip **switch 1** to the configuration position.
2. Turn the NCR 7197 off.
3. Press and hold the **Paper Feed** button.
4. While continuing to hold the Paper Feed button, turn the printer on.
5. Keep holding the button until the printer enters configuration mode and prints the configuration menu.
6. Release the Paper Feed button.

Use short and long presses of the Paper Feed button to navigate and confirm selections shown on the printed menu.

After saving the settings and leaving configuration mode:

1. Turn the printer off.
2. Return **switch 1** to its normal operating position.
3. Turn the printer on normally.

### Restore the physical printer to its defaults

1. Enter the NCR 7197 configuration menu using the Paper Feed button.
2. Select **Set EEPROM To Default**.
3. Confirm the reset.
4. Allow the printer to restart.
5. Enter the configuration menu again.

Restoring EEPROM defaults clears the previous emulation and communication settings. Configure the required values again after the reset.

Set:

- Printer Emulation: **7194 Mode**
- Printer ID: **Emulated Printer ID**
- Baud rate: **9600**

Save the settings and restart the printer.

Print the printer configuration paper and confirm that it reports:

```text
Emulation: 7194
Printer ID: Emulated
Baud Rate: 9600
```

The baud rate shown on this printer configuration paper is the value that must be used in Windows.

## 4. Match the Windows COM-port baud rate

1. Open **Device Manager**.
2. Expand **Ports (COM & LPT)**.
3. Open the properties for the Edge/EPIC COM port used by the printer.
4. Open **Port Settings**.
5. Set **Bits per second** to the baud rate printed on the NCR configuration paper.
6. Use:
   - Data bits: **8**
   - Parity: **None**
   - Stop bits: **1**
   - Flow control: use the setting required by the NCR configuration
7. Click **Apply**, then **OK**.

Example:

```text
Printer configuration paper: 9600 baud
Windows Edge/EPIC COM port: 9600 baud
```

If these values do not match, printing may fail or produce gibberish.

## 5. Configure and name the Windows printer queue

1. Open **Control Panel → Devices and Printers**.
2. Right-click the NCR printer.
3. Select **Printer properties**.
4. Confirm on the **Ports** tab that the selected port is the Edge/EPIC COM port.
5. On the **General** tab, rename the printer for full-size Odoo receipts:

```text
NCR7197Receipt__IMC_SCALE100__
```

6. Click **Apply**, then **OK**.
7. Open the printer’s **Advanced** tab.
8. Confirm:
   - Print Processor: **winprint**
   - Default data type: **RAW**

### Set the NCR queue as the Windows default printer

1. Open **Windows Settings → Devices → Printers & scanners**.
2. Turn off **Let Windows manage my default printer**.
3. Select:

```text
NCR7197Receipt__IMC_SCALE100__
```

4. Click **Manage**.
5. Click **Set as default**.

You can also use **Control Panel → Devices and Printers**, right-click the NCR queue, and choose **Set as default printer**.

The default-printer setting does not replace the Odoo POS receipt-printer mapping. The same NCR queue must still be selected in the Odoo POS configuration.

### Meaning of the printer name

- `IMC` tells Odoo to use `ESC *` column-image compatibility mode instead of Epson `GS v 0` raster mode.
- `SCALE100` prints the receipt at full size.

If full-size printing is too slow for the configured serial baud rate, smaller sizes can be tested:

```text
NCR7197Receipt__IMC_SCALE70__
NCR7197Receipt__IMC_SCALE50__
```

Rename the queue, restart Odoo IoT, and remap the printer in Odoo after every name change.

## 6. Restart Odoo IoT

Open PowerShell as Administrator and run:

```powershell
Restart-Service odoo-iot -Force
```

Wait approximately 30–60 seconds for device discovery.

## 7. Confirm detection by Odoo IoT

Open the Windows Odoo IoT status page:

```text
http://127.0.0.1:8069/status
```

Confirm that this device appears:

```text
NCR7197Receipt__IMC_SCALE100__
```

If it does not appear:

1. Confirm the Windows printer queue exists.
2. Restart `odoo-iot`.
3. Wait one minute.
4. Refresh the IoT status page and the Odoo IoT device page.

## 8. Map the printer to the correct Odoo POS

The Odoo IoT printer test and the live POS receipt-printer setting are separate.

1. In Odoo, open **Point of Sale → Configuration → Settings**.
2. Open the exact POS configuration being used.
3. Find **Connected Devices / IoT**.
4. Set **Receipt Printer** to:

```text
NCR7197Receipt__IMC_SCALE100__
```

5. Make sure it is not set to:

```text
Generic / Text Only
```

6. Save the POS configuration.
7. Completely close the running POS session.
8. Open a new POS session.

An already-open POS session can keep the previous printer mapping in its browser cache.

## 9. Test in the correct order

### Odoo IoT printer test

Run the printer test from the Odoo IoT device page.

Expected output:

```text
IoT Box Test Receipt
```

This confirms that Odoo can reach the queue and that basic text and cut commands work.

### Full POS receipt

Complete a test sale in the newly opened POS session and print its receipt.

The full receipt is sent as an image. This is why the `IMC` option is required.

## 10. Troubleshooting

### The IoT test prints, but the POS receipt does not

The live POS is probably mapped to a different printer.

Check the POS configuration and confirm that its **Receipt Printer** field is set to the NCR queue—not **Generic / Text Only**.

Save the setting, close the POS session completely, and open a new session.

### The receipt prints as gibberish

Confirm:

- NCR emulation is **7194 Mode**.
- Printer ID is **Emulated**.
- The queue name is `NCR7197Receipt__IMC_SCALE100__`.
- The live POS is mapped to that exact queue.

### The receipt is too small

Increase the scale gradually:

```text
NCR7197Receipt__IMC_SCALE60__
NCR7197Receipt__IMC_SCALE70__
NCR7197Receipt__IMC_SCALE100__
```

Restart Odoo IoT and remap the POS printer after each rename.

### The receipt stops, disappears, or takes too long

If full-size printing cannot finish reliably at the selected baud rate, use:

```text
NCR7197Receipt__IMC_SCALE50__
```

Large bitmap receipts can take too long over a 9600-baud serial connection.

### Odoo IoT takes a long time to detect devices

Restart the service:

```powershell
Restart-Service odoo-iot -Force
```

Also confirm that the computer can reach the configured Odoo server over HTTPS.

### COM7 is busy or access is denied

Only the Windows printer spooler should control COM7.

Avoid configuring barcode or other serial-reader services to monitor COM7. Restrict the barcode wedge service to the scanner’s actual port, which was detected as COM1 on this computer.

## Working configuration used on this computer

```text
Printer: NCR 7197
Printer emulation: 7194 Mode
Printer ID: Emulated
Windows port: COM7
Serial baud: 9600
Windows print processor: winprint
Windows datatype: RAW
Queue name: NCR7197Receipt__IMC_SCALE100__
Odoo image mode: ESC * column image
```
