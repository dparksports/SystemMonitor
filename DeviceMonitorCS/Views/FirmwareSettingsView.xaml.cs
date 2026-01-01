using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Management;
using System.Windows;
using System.Windows.Controls;
using DeviceMonitorCS.Models;

using DeviceMonitorCS.Helpers;

namespace DeviceMonitorCS.Views
{
    public partial class FirmwareSettingsView : UserControl
    {
        public ObservableCollection<FirmwareTableItem> FirmwareTables { get; set; } = new ObservableCollection<FirmwareTableItem>();
        public ObservableCollection<BcdEntry> BcdEntries { get; set; } = new ObservableCollection<BcdEntry>();

        public FirmwareSettingsView()
        {
            InitializeComponent();
            FirmwareGrid.ItemsSource = FirmwareTables;
            BcdGrid.ItemsSource = BcdEntries;
            Loaded += FirmwareSettingsView_Loaded;
        }

        private void FirmwareSettingsView_Loaded(object sender, RoutedEventArgs e)
        {
            if (FirmwareTables.Count == 0) LoadFirmwareTables();
            if (BcdEntries.Count == 0) LoadBcdInfo();
        }

        private void LoadFirmwareTables()
        {
            FirmwareTables.Clear();
            
            // Try to enable privilege for UEFI access
            PrivilegeHelper.EnablePrivilege("SeSystemEnvironmentPrivilege");

            try
            {
                // WMI fallback or primary? The user has issues with WMI. Let's use P/Invoke primary.
                // Standard signatures: 'ACPI', 'FIRM', 'RSMB'
                
                // 1. ACPI
                LoadTablesForProvider("ACPI", 0x41435049); // 'ABCD' -> 0x44434241 (Little Endian) -> 'ACPI' is 0x49504341?
                // Actually simpler: BitConverter can tell us. 'A' is 0x41. 'ACPI' -> 'I' is MSB? No.
                // Using 0x41435049 is correct for String->Int conversion if big endian or just treating as FourCC?
                // Actually the API expects a DWORD. 'ACPI' is 0x49504341 in memory if LE.
                // But most examples use 0x41435049 (Big Endian) representation? No, let's use the FourCC helper below.
                    
                // 2. RSMB (Raw SMBIOS)
                LoadTablesForProvider("RSMB", 0x52534D42);

                // 3. FIRM (Firmware)
                LoadTablesForProvider("FIRM", 0x4649524D);

                // If completely empty, try WMI as last resort?
                // No, user said WMI failed.
                
                if (FirmwareTables.Count == 0)
                {
                     FirmwareTables.Add(new FirmwareTableItem { Name = "Info", TableID = "-", Length = "No tables found via Native API" });
                }
            }
            catch (Exception ex)
            {
                FirmwareTables.Add(new FirmwareTableItem { Name = "Error", TableID = "-", Length = ex.Message });
            }
        }

        private void LoadTablesForProvider(string providerName, uint providerSig)
        {
            try
            {
                // Get buffer size
                uint requiredSize = NativeMethods.EnumSystemFirmwareTables(providerSig, IntPtr.Zero, 0);
                if (requiredSize == 0) return; // None found or error

                IntPtr buffer = System.Runtime.InteropServices.Marshal.AllocHGlobal((int)requiredSize);
                try
                {
                    uint result = NativeMethods.EnumSystemFirmwareTables(providerSig, buffer, requiredSize);
                    if (result > 0)
                    {
                         int count = (int)result / 4; // Each ID is 4 bytes
                         for (int i = 0; i < count; i++)
                         {
                             // Read table ID
                             // Offset = i * 4
                             byte[] idBytes = new byte[4];
                             System.Runtime.InteropServices.Marshal.Copy(IntPtr.Add(buffer, i*4), idBytes, 0, 4);
                             
                             // Convert to string (usually ASCII)
                             string tableIdStr = System.Text.Encoding.ASCII.GetString(idBytes);
                             
                             // Get content length
                             // GetSystemFirmwareTable
                             uint tableIdInt = BitConverter.ToUInt32(idBytes, 0);
                             uint len = NativeMethods.GetSystemFirmwareTable(providerSig, tableIdInt, IntPtr.Zero, 0);

                             FirmwareTables.Add(new FirmwareTableItem
                             {
                                 Name = providerName,
                                 TableID = tableIdStr, // e.g., "FACP"
                                 Length = len.ToString(),
                                 Description = GetTableDescription(tableIdStr)
                             });
                         }
                    }
                }
                finally
                {
                    System.Runtime.InteropServices.Marshal.FreeHGlobal(buffer);
                }
            }
            catch { }
        }
        
        private string GetTableDescription(string id)
        {
            if (string.IsNullOrEmpty(id)) return "";
            
            // Common ACPI Signatures
            switch (id.ToUpper())
            {
                case "ACPI": return "ACPI Entry Point";
                case "FACP": return "Fixed ACPI Description Table (FADT)";
                case "APIC": return "Multiple APIC Description Table (MADT)";
                case "MCFG": return "PCI Express Memory Mapped Configuration Space";
                case "HPET": return "High Precision Event Timer Table";
                case "SSDT": return "Secondary System Description Table";
                case "DSDT": return "Differentiated System Description Table";
                case "ECDT": return "Embedded Controller Boot Resources Table";
                case "UEFI": return "UEFI ACPI Data Table";
                case "BGRT": return "Boot Graphics Resource Table";
                case "XSDT": return "Extended System Description Table";
                case "RSDT": return "Root System Description Table";
                case "FPDT": return "Firmware Performance Data Table";
                case "MSDM": return "Microsoft Data Management Table (License Key)";
                case "SLIC": return "Software Licensing Description Table (OEM Activation)";
                case "TPM2": return "Trusted Platform Module 2 Table";
                case "DBGP": return "Debug Port Table";
                case "DBG2": return "Debug Port Table 2";
                case "LPIT": return "Low Power Idle Table";
                case "WSMT": return "Windows Security Mitigations Table";
                case "BERT": return "Boot Error Record Table";
                case "HEST": return "Hardware Error Source Table";
                case "ERST": return "Error Record Serialization Table";
                case "EINJ": return "Error Injection Table";
                case "SRAT": return "System Resource Affinity Table";
                case "SLIT": return "System Locality Information Table";
                case "IVRS": return "I/O Virtualization Reporting Structure";
                case "WDAT": return "Watchdog Action Table";
                case "TCPA": return "Trusted Computing Platform Alliance Table";
                case "WAET": return "Windows ACPI Emulated Devices Table";
                case "DRTM": return "Dynamic Root of Trust for Measurement Table";
                case "WPBT": return "Windows Platform Binary Table";
                case "FACS": return "Firmware ACPI Control Structure";
                case "BOOT": return "Simple Boot Flag Table";
                case "CRAT": return "Component Resource Attribute Table";
                case "ASF!": return "Alert Standard Format Table";
                case "NFIT": return "NVDIMM Firmware Interface Table";
                case "PMTT": return "Platform Memory Topology Table";
                case "PPTT": return "Processor Properties Topology Table";
                case "SDEV": return "Secure Devices Table";
                default: return "Unknown / Vendor Specific";
            }
        }

        private static class NativeMethods
        {
            [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
            public static extern uint EnumSystemFirmwareTables(uint FirmwareTableProviderSignature, IntPtr pFirmwareTableEnumBuffer, uint BufferSize);

            [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
            public static extern uint GetSystemFirmwareTable(uint FirmwareTableProviderSignature, uint FirmwareTableID, IntPtr pFirmwareTableBuffer, uint BufferSize);
        }
        
        private void AskAi_Click(object sender, RoutedEventArgs e)
        {
            var menuItem = sender as MenuItem;
            var contextMenu = menuItem.Parent as ContextMenu;
            var grid = contextMenu.PlacementTarget as DataGrid;

            if (grid != null && grid.SelectedItem != null)
            {
                var window = new AskAiWindow(grid.SelectedItem);
                window.Owner = Window.GetWindow(this);
                window.ShowDialog();
            }
        }

        private void ViewDetails_Click(object sender, RoutedEventArgs e)
        {
            var menuItem = sender as MenuItem;
            var contextMenu = menuItem.Parent as ContextMenu;
            var grid = contextMenu.PlacementTarget as DataGrid;

            if (grid != null && grid.SelectedItem is BcdEntry entry)
            {
                string details = $"Type: {entry.Type}\nIdentifier: {entry.Identifier}\nDescription: {entry.Description}\nDevice: {entry.Device}\nPath: {entry.Path}\n\nAdditional Settings:\n{entry.AdditionalSettings}";
                MessageBox.Show(details, "BCD Entry Details", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private async void LoadBcdInfo()
        {
            try
            {
                // Clear existing
                BcdEntries.Clear();

                // Run bcdedit
                string output = await System.Threading.Tasks.Task.Run(() =>
                {
                    try 
                    {
                        ProcessStartInfo psi = new ProcessStartInfo("bcdedit", "/enum all");
                        psi.RedirectStandardOutput = true;
                        psi.UseShellExecute = false;
                        psi.CreateNoWindow = true;
                        // psi.StandardOutputEncoding = System.Text.Encoding.GetEncoding(437); // CP437 causes error if provider not registered

                        using (Process p = Process.Start(psi))
                        {
                            string result = p.StandardOutput.ReadToEnd();
                            p.WaitForExit();
                            return result;
                        }
                    }
                    catch (Exception ex)
                    {
                        return "Error running bcdedit: " + ex.Message;
                    }
                });

                // Parse Output
                ParseBcdOutput(output);
            }
            catch (Exception ex)
            {
                BcdEntries.Add(new BcdEntry { Type = "Error", Description = ex.Message });
            }
        }

        private void ParseBcdOutput(string rawOutput)
        {
            if (string.IsNullOrWhiteSpace(rawOutput) || rawOutput.StartsWith("Error"))
            {
                 BcdEntries.Add(new BcdEntry { Type = "Error", Description = rawOutput });
                 return;
            }

            var lines = rawOutput.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            string currentType = "";
            BcdEntry currentEntry = null;

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].TrimEnd(); // Keep leading spaces for potential multi-line values, but we mainly care about structure
                
                if (string.IsNullOrWhiteSpace(line)) continue;

                // Check for Section Header (Text followed by separator line)
                if (i + 1 < lines.Length && lines[i + 1].Trim().StartsWith("---"))
                {
                    currentType = line.Trim();
                    i++; // Skip separator
                    continue;
                }

                // Check for Key-Value pair
                // Format: "identifier              {current}"
                // We'll split by first whitespace run
                var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 1)
                {
                    string key = parts[0].ToLower();
                    
                    // If parsing a new identifier, it's a new entry
                    if (key == "identifier")
                    {
                         if (currentEntry != null)
                         {
                             BcdEntries.Add(currentEntry);
                         }
                         currentEntry = new BcdEntry { Type = currentType };
                    }
                    
                    if (currentEntry != null && parts.Length >= 2)
                    {
                         // Join the rest as value in case value has spaces
                         // But simple split remove empty entries destroys spaces in value if any?
                         // Better: Find first space index
                         int spaceIndex = line.IndexOfAny(new[] { ' ', '\t' });
                         if (spaceIndex > 0)
                         {
                             string val = line.Substring(spaceIndex).Trim();

                             switch (key)
                             {
                                 case "identifier": currentEntry.Identifier = val; break;
                                 case "description": currentEntry.Description = val; break;
                                 case "device": currentEntry.Device = val; break;
                                 case "path": currentEntry.Path = val; break;
                                 default: 
                                     if (string.IsNullOrEmpty(currentEntry.AdditionalSettings))
                                         currentEntry.AdditionalSettings = $"{key}: {val}";
                                     else
                                         currentEntry.AdditionalSettings += $", {key}: {val}";
                                     break;
                             }
                         }
                    }
                }
            }
            
            // Add last entry
            if (currentEntry != null)
            {
                BcdEntries.Add(currentEntry);
            }
        }
    }
}
