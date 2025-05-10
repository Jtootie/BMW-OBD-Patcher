import tkinter as tk
from tkinter import filedialog, scrolledtext, messagebox
import os

class BMWPatcherApp:
    def __init__(self, root):
        self.root = root
        self.root.title("BMW iRom Patcher")
        self.filepath = None
        self.bin_data = None
        self.detection = None
        self.patch_info = {}

        # Add About button (displaying a "?" symbol) at the top right corner.
        about_button = tk.Button(root, text="?", command=self.show_about, width=2, relief="flat")
        about_button.place(relx=1.0, x=-10, y=10, anchor="ne")
        
        # Permanent Footer: Display the freeware disclaimer at the bottom
        self.footer_label = tk.Label(root, 
                                     text="THIS PROGRAM IS FREEWARE, DO NOT PAY FOR IT!", 
                                     font=("Arial", 10, "bold"),
                                     fg="red")
        self.footer_label.pack(side="bottom", fill="x", pady=(0, 5))
        
        # GUI Layout
        tk.Button(root, text="Load BIN", command=self.load_bin).pack(pady=5)
        self.detect_label = tk.Label(root, text="No file loaded")
        self.detect_label.pack()

        tk.Button(root, text="Patch BIN", command=self.patch_bin, state="disabled", name="patch_btn").pack(pady=5)
        tk.Button(root, text="Save BIN As...", command=self.save_bin_as, state="disabled", name="save_btn").pack(pady=5)

        self.output = scrolledtext.ScrolledText(root, width=60, height=15)
        self.output.pack(padx=10, pady=10)
    
    def show_about(self):
        """Display the About pop-up."""
        messagebox.showinfo("About", "Created by O.S. Automotives and Jtooties Garage")
        

    def log(self, text):
        self.output.insert(tk.END, text + "\n")
        self.output.see(tk.END)

    def load_bin(self):
        path = filedialog.askopenfilename(filetypes=[("Binary files", "*.bin")])
        if not path:
            return

        # Clear the log window when a new BIN is loaded
        self.output.delete('1.0', tk.END)

        with open(path, "rb") as f:
            self.bin_data = bytearray(f.read())
        self.filepath = path

        # Detect patch type based on presence of specific byte sequences
        if b'\x80\x2A\x03\xE2\x07' in self.bin_data or b'\x80\x48\x03\x44\x00' in self.bin_data:
            self.detection = "Gen1 iRom"
            self.patch_info = {
                "unlock_offset": 0x40260,
                "unlock_patch": b'\x39\x7E\xB6\x88',
                "unlock_check": b'\x00\x00\x00\x00',
                "unlock_applied": b'\x39\x7E\xB6\x88',
                "obd_find": b'\x80\x2A\x03\xE2\x07',
                "obd_patch": b'\x80\x48\x03\x44\x00'
            }
        elif b'\x91\x10\x00\x26\xF6\x27' in self.bin_data or b'\x91\x10\x00\x26\x82\x02' in self.bin_data:
            self.detection = "Gen2 iRom"
            self.patch_info = {
                "unlock_offset": 0x5F7DC,
                "unlock_patch": b'\x38\xD1\xBF\xDC',
                "unlock_check": b'\x00\x00\x00\x00',
                "unlock_applied": b'\x38\xD1\xBF\xDC',
                "obd_find": b'\x91\x10\x00\x26\xF6\x27',
                "obd_patch": b'\x91\x10\x00\x26\x82\x02'
            }
        else:
            self.detect_label.config(text="Unknown file / patch not supported")
            self.log("Failed to detect supported patch sequence.")
            return

        self.detect_label.config(text=f"Detected: {self.detection}")
        self.log(f"Loaded: {os.path.basename(self.filepath)}")
        self.log(f"Detected: {self.detection}")

        # Read and log firmware version (10 bytes at offset 0x164)
        if len(self.bin_data) >= 0x164 + 10:
            firmware_bytes = self.bin_data[0x164:0x164 + 10]
            firmware_text = firmware_bytes.decode('utf-8', errors='replace')
            self.log("Firmware version: " + firmware_text)
        else:
            self.log("File too short for firmware version info (offset 0x164).")

        # Read and log current PRG (7 bytes at offset 0x80145) with single spacing
        if len(self.bin_data) >= 0x80145 + 7:
            current_prg_bytes = self.bin_data[0x80145:0x80145 + 7]
            current_prg_str = " ".join(f"{b:02X}" for b in current_prg_bytes)
            self.log("Current PRG: " + current_prg_str)
        else:
            self.log("File too short for Current PRG info (offset 0x80145).")

        # --- New Feature: Engine and Chassis Information ---
        self.check_engine_chassis()

        self.root.children["patch_btn"].config(state="normal")
        self.root.children["save_btn"].config(state="normal")

    def check_engine_chassis(self):
        if not self.bin_data:
            return

        if self.detection == "Gen1 iRom":
            # Check Engine information at offset 0x7BFE58 (3 bytes)
            engine_offset = 0x7BFE58
            if len(self.bin_data) >= engine_offset + 3:
                engine_bytes = self.bin_data[engine_offset:engine_offset + 3]
                engine_text = engine_bytes.decode('utf-8', errors='replace')
                self.log("Engine: " + engine_text)
            else:
                self.log("File too short for Gen1 Engine info (offset 0x7BFE58).")

            # Check Chassis information at offset 0x7BFE68 (3 bytes)
            chassis_offset = 0x7BFE68
            if len(self.bin_data) >= chassis_offset + 3:
                chassis_bytes = self.bin_data[chassis_offset:chassis_offset + 3]
                chassis_text = chassis_bytes.decode('utf-8', errors='replace')
                self.log("Chassis: " + chassis_text)
            else:
                self.log("File too short for Gen1 Chassis info (offset 0x7BFE68).")

        elif self.detection == "Gen2 iRom":
            # First check the three bytes at offset 0x7FFE59
            candidate1_offset = 0x7FFE59
            if len(self.bin_data) >= candidate1_offset + 3:
                candidate1_bytes = self.bin_data[candidate1_offset:candidate1_offset + 3]
                candidate1_text = candidate1_bytes.decode('utf-8', errors='replace')
                if candidate1_text in ("B46", "B48"):
                    self.log("Engine: " + candidate1_text)
                    # Read Chassis info at offset 0x7FFE62 (3 bytes)
                    chassis_offset = 0x7FFE62
                    if len(self.bin_data) >= chassis_offset + 3:
                        chassis_bytes = self.bin_data[chassis_offset:chassis_offset + 3]
                        chassis_text = chassis_bytes.decode('utf-8', errors='replace')
                        self.log("Chassis: " + chassis_text)
                    else:
                        self.log("File too short for Gen2 Chassis info (offset 0x7FFE62).")
                else:
                    # Alternate candidate from offset 0x7FFE5B
                    candidate2_offset = 0x7FFE5B
                    if len(self.bin_data) >= candidate2_offset + 3:
                        candidate2_bytes = self.bin_data[candidate2_offset:candidate2_offset + 3]
                        candidate2_text = candidate2_bytes.decode('utf-8', errors='replace')
                        if candidate2_text in ("B58", "S58", "S63"):
                            self.log("Engine: " + candidate2_text)
                            if candidate2_text in ("B58", "S58"):
                                # Read Chassis info at offset 0x7FFE64 (3 bytes)
                                chassis_offset = 0x7FFE64
                                if len(self.bin_data) >= chassis_offset + 3:
                                    chassis_bytes = self.bin_data[chassis_offset:chassis_offset + 3]
                                    chassis_text = chassis_bytes.decode('utf-8', errors='replace')
                                    self.log("Chassis: " + chassis_text)
                                else:
                                    self.log("File too short for Gen2 Chassis info (offset 0x7FFE64).")
                            elif candidate2_text == "S63":
                                # Read Chassis info at offset 0x7FFE61 (4 bytes)
                                chassis_offset = 0x7FFE61
                                if len(self.bin_data) >= chassis_offset + 4:
                                    chassis_bytes = self.bin_data[chassis_offset:chassis_offset + 4]
                                    chassis_text = chassis_bytes.decode('utf-8', errors='replace')
                                    self.log("Chassis: " + chassis_text)
                                else:
                                    self.log("File too short for Gen2 Chassis info (offset 0x7FFE61).")
                        else:
                            self.log("Gen2 alternate engine candidate did not match expected codes.")
                    else:
                        self.log("File too short for Gen2 alternate engine info (offset 0x7FFE5B).")
            else:
                self.log("File too short for Gen2 candidate check (offset 0x7FFE59).")
        else:
            self.log("Detection not recognized for engine/chassis check.")

    def patch_bin(self):
        if not self.patch_info:
            return

        offset = self.patch_info["unlock_offset"]
        expected = self.patch_info["unlock_check"]
        applied = self.patch_info["unlock_applied"]
        patch = self.patch_info["unlock_patch"]

        current_bytes = self.bin_data[offset:offset + 4]
        if current_bytes == patch:
            self.log(f"Unlock patch already applied at 0x{offset:X}")
        elif current_bytes != expected:
            self.log(f"Warning: Unexpected bytes at unlock offset 0x{offset:X}")
            return
        else:
            self.bin_data[offset:offset + 4] = patch
            self.log(f"Applied unlock patch at 0x{offset:X}")

        obd_offset = self.bin_data.find(self.patch_info["obd_find"])
        if obd_offset == -1:
            # Check if already patched
            if self.bin_data.find(self.patch_info["obd_patch"]) != -1:
                self.log("OBD patch already applied.")
            else:
                self.log("Error: OBD patch sequence not found.")
            return
        self.bin_data[obd_offset:obd_offset + len(self.patch_info["obd_patch"])] = self.patch_info["obd_patch"]
        self.log(f"Applied OBD patch at 0x{obd_offset:X}")
        self.log("Patching completed. Use 'Save BIN As...' to save the file.")

    def save_bin_as(self):
        if not self.bin_data:
            return
        new_path = filedialog.asksaveasfilename(defaultextension=".bin", filetypes=[("Binary files", "*.bin")])
        if new_path:
            with open(new_path, "wb") as f:
                f.write(self.bin_data)
            self.log(f"Saved patched BIN as: {os.path.basename(new_path)}")
            messagebox.showinfo("Saved", f"BIN saved as:\n{os.path.basename(new_path)}")

if __name__ == "__main__":
    root = tk.Tk()
    app = BMWPatcherApp(root)
    root.mainloop()
