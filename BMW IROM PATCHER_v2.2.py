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

        self.original_bin_data = None
        self.tuned_bin_data = None

        # About button (top right)
        about_button = tk.Button(root, text="?", command=self.show_about, width=2, relief="flat")
        about_button.place(relx=1.0, x=-10, y=10, anchor="ne")

        # Freeware footer
        self.footer_label = tk.Label(root,
                                     text="THIS PROGRAM IS FREEWARE, DO NOT PAY FOR IT!",
                                     font=("Arial", 10, "bold"),
                                     fg="red")
        self.footer_label.pack(side="bottom", fill="x", pady=(0, 5))

        # Load & patch controls
        tk.Button(root, text="Load BIN", command=self.load_bin).pack(pady=5)
        self.detect_label = tk.Label(root, text="No file loaded")
        self.detect_label.pack()

        tk.Button(root, text="Patch BIN", command=self.patch_bin, state="disabled", name="patch_btn").pack(pady=5)
        tk.Button(root, text="Save BIN As...", command=self.save_bin_as, state="disabled", name="save_btn").pack(pady=5)

        # Gen1-exclusive button row
        self.gen1_frame = tk.Frame(root)
        self.gen1_frame.pack(pady=5)

        self.original_btn = tk.Button(self.gen1_frame, text="Original bin", command=self.load_original_bin, state="disabled")
        self.original_btn.pack(side="left", padx=5)

        self.tuned_btn = tk.Button(self.gen1_frame, text="Tuned bin", command=self.load_tuned_bin, state="disabled")
        self.tuned_btn.pack(side="left", padx=5)

        self.convert_btn = tk.Button(self.gen1_frame, text="Convert", command=self.convert_bin, state="disabled")
        self.convert_btn.pack(side="left", padx=5)

        self.revert_btn = tk.Button(self.gen1_frame, text="Revert", command=self.revert_bin, state="disabled")
        self.revert_btn.pack(side="left", padx=5)

        # Output log
        self.output = scrolledtext.ScrolledText(root, width=60, height=15)
        self.output.pack(padx=10, pady=10)

    def show_about(self):
        messagebox.showinfo("About", "Created by O.S. Automotives and Jtooties Garage")

    def log(self, text):
        self.output.insert(tk.END, text + "\n")
        self.output.see(tk.END)

    def load_bin(self):
        path = filedialog.askopenfilename(filetypes=[("Binary files", "*.bin")])
        if not path:
            return

        self.output.delete('1.0', tk.END)

        with open(path, "rb") as f:
            self.bin_data = bytearray(f.read())
        self.filepath = path

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

            self.original_btn.config(state="normal")
            self.tuned_btn.config(state="normal")
            self.convert_btn.config(state="normal")
            self.revert_btn.config(state="normal")

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

            self.original_btn.config(state="disabled")
            self.tuned_btn.config(state="disabled")
            self.convert_btn.config(state="disabled")
            self.revert_btn.config(state="disabled")
        else:
            self.detect_label.config(text="Unknown file / patch not supported")
            self.log("Failed to detect supported patch sequence.")
            return

        self.detect_label.config(text=f"Detected: {self.detection}")
        self.log(f"Loaded: {os.path.basename(self.filepath)}")
        self.log(f"Detected: {self.detection}")

        # Firmware version
        if len(self.bin_data) >= 0x164 + 10:
            fw = self.bin_data[0x164:0x164 + 10].decode('utf-8', errors='replace')
            self.log("Firmware version: " + fw)
        else:
            self.log("File too short for firmware version info (offset 0x164).")

        # Current PRG
        if len(self.bin_data) >= 0x80145 + 7:
            prg = " ".join(f"{b:02X}" for b in self.bin_data[0x80145:0x80145 + 7])
            self.log("Current PRG: " + prg)
        else:
            self.log("File too short for Current PRG info (offset 0x80145).")

        self.check_engine_chassis()
        self.root.children["patch_btn"].config(state="normal")
        self.root.children["save_btn"].config(state="normal")

    def load_original_bin(self):
        path = filedialog.askopenfilename(filetypes=[("Binary files", "*.bin")])
        if path:
            with open(path, "rb") as f:
                self.original_bin_data = f.read()
            self.log(f"Original bin loaded: {os.path.basename(path)}")

    def load_tuned_bin(self):
        path = filedialog.askopenfilename(filetypes=[("Binary files", "*.bin")])
        if path:
            with open(path, "rb") as f:
                self.tuned_bin_data = bytearray(f.read())
            self.log(f"Tuned bin loaded: {os.path.basename(path)}")

    def convert_bin(self):
        if not self.original_bin_data or not self.tuned_bin_data:
            messagebox.showwarning("Warning", "Original or Tuned bin not loaded.")
            return

        if self.original_bin_data[0xD00:0xD03] != b'CB_':
            messagebox.showwarning("Warning", "Original BIN does not contain CB_ marker at 0xD00.")
            return

        insert_data = self.original_bin_data[0x0:0xD00]
        del self.tuned_bin_data[0:0x40D00]
        self.tuned_bin_data = bytearray(insert_data) + self.tuned_bin_data
        self.save_custom_bin("Save Converted BIN As...")

    def revert_bin(self):
        if not self.tuned_bin_data or not self.bin_data:
            messagebox.showwarning("Warning", "Tuned BIN or main loaded BIN not available.")
            return

        if self.tuned_bin_data[0xD00:0xD03] != b'CB_':
            messagebox.showwarning("Warning", "Tuned BIN does not contain CB_ marker at 0xD00.")
            return

        insert_data = self.bin_data[0:0x40D00]
        del self.tuned_bin_data[0:0xD00]
        self.tuned_bin_data = bytearray(insert_data) + self.tuned_bin_data
        self.save_custom_bin("Save Reverted BIN As...")

    def save_custom_bin(self, title="Save BIN As..."):
        path = filedialog.asksaveasfilename(defaultextension=".bin", title=title, filetypes=[("Binary files", "*.bin")])
        if path:
            with open(path, "wb") as f:
                f.write(self.tuned_bin_data)
            self.log(f"Saved: {os.path.basename(path)}")
            messagebox.showinfo("Saved", f"BIN saved as:\n{os.path.basename(path)}")

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
        path = filedialog.asksaveasfilename(defaultextension=".bin", filetypes=[("Binary files", "*.bin")])
        if path:
            with open(path, "wb") as f:
                f.write(self.bin_data)
            self.log(f"Saved patched BIN as: {os.path.basename(path)}")
            messagebox.showinfo("Saved", f"BIN saved as:\n{os.path.basename(path)}")

    def check_engine_chassis(self):
        if not self.bin_data:
            return

        if self.detection == "Gen1 iRom":
            if len(self.bin_data) >= 0x7BFE58 + 3:
                engine = self.bin_data[0x7BFE58:0x7BFE58 + 3].decode('utf-8', errors='replace')
                self.log("Engine: " + engine)
            else:
                self.log("File too short for Gen1 Engine info (offset 0x7BFE58).")

            if len(self.bin_data) >= 0x7BFE68 + 3:
                chassis = self.bin_data[0x7BFE68:0x7BFE68 + 3].decode('utf-8', errors='replace')
                self.log("Chassis: " + chassis)
            else:
                self.log("File too short for Gen1 Chassis info (offset 0x7BFE68).")

        elif self.detection == "Gen2 iRom":
            if len(self.bin_data) >= 0x7FFE59 + 3:
                candidate1 = self.bin_data[0x7FFE59:0x7FFE59 + 3].decode('utf-8', errors='replace')
                if candidate1 in ("B46", "B48"):
                    self.log("Engine: " + candidate1)
                    if len(self.bin_data) >= 0x7FFE62 + 3:
                        chassis = self.bin_data[0x7FFE62:0x7FFE62 + 3].decode('utf-8', errors='replace')
                        self.log("Chassis: " + chassis)
                elif len(self.bin_data) >= 0x7FFE5B + 3:
                    candidate2 = self.bin_data[0x7FFE5B:0x7FFE5B + 3].decode('utf-8', errors='replace')
                    if candidate2 in ("B58", "S58"):
                        self.log("Engine: " + candidate2)
                        if len(self.bin_data) >= 0x7FFE64 + 3:
                            chassis = self.bin_data[0x7FFE64:0x7FFE64 + 3].decode('utf-8', errors='replace')
                            self.log("Chassis: " + chassis)
                    elif candidate2 == "S63":
                        if len(self.bin_data) >= 0x7FFE61 + 4:
                            chassis = self.bin_data[0x7FFE61:0x7FFE61 + 4].decode('utf-8', errors='replace')
                            self.log("Chassis: " + chassis)

if __name__ == "__main__":
    root = tk.Tk()
    app = BMWPatcherApp(root)
    root.mainloop()
