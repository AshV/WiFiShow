import subprocess
import tkinter as tk
from tkinter import messagebox, filedialog
import threading
import csv

# We use customtkinter instead of standard ttk for a modern, touch-friendly UI
import customtkinter as ctk

# Ensure qrcode is available
try:
    import qrcode
    from PIL import ImageTk, Image
    QR_AVAILABLE = True
except ImportError:
    QR_AVAILABLE = False

ctk.set_appearance_mode("System")  # Modes: "System" (standard), "Dark", "Light"
ctk.set_default_color_theme("blue")  # Themes: "blue" (standard), "green", "dark-blue"

def get_wifi_profiles():
    try:
        output = subprocess.check_output(['netsh', 'wlan', 'show', 'profiles'], creationflags=subprocess.CREATE_NO_WINDOW).decode('utf-8', errors='ignore')
        profiles = []
        for line in output.split('\n'):
            if "All User Profile" in line:
                parts = line.split(":")
                if len(parts) > 1:
                    profiles.append(parts[1].strip())
        return profiles
    except Exception as e:
        return []

def get_wifi_password(profile):
    try:
        output = subprocess.check_output(
            ['netsh', 'wlan', 'show', 'profile', profile, 'key=clear'], 
            creationflags=subprocess.CREATE_NO_WINDOW,
            stderr=subprocess.STDOUT
        ).decode('utf-8', errors='ignore')
        
        password = ""
        auth_type = ""
        security_key = ""
        
        for line in output.split('\n'):
            line_stripped = line.strip()
            if line_stripped.startswith("Authentication"):
                parts = line_stripped.split(":")
                if len(parts) > 1:
                    auth_type = parts[1].strip()
            elif line_stripped.startswith("Security key"):
                parts = line_stripped.split(":")
                if len(parts) > 1:
                    security_key = parts[1].strip()
            elif line_stripped.startswith("Key Content"):
                parts = line_stripped.split(":")
                if len(parts) > 1:
                    password = parts[1].strip()
                    break
        
        if password:
            return password
            
        if "Open" in auth_type:
            return "Open Network (No Password)"
        elif "Enterprise" in auth_type:
            return "Enterprise Network (Username/Cert)"
        elif security_key == "Present":
            return "Requires Admin Rights"
        elif security_key == "Absent":
            return "Password Not Saved"
            
        # If no recognized pattern, return the first meaningful line of output as error detail
        for line in output.split('\n'):
            line_stripped = line.strip()
            if line_stripped and not line_stripped.startswith("Profile") and not line_stripped.startswith("====="):
                return f"Detail: {line_stripped}"
                
        return "Unknown Status"
    except subprocess.CalledProcessError as e:
        error_out = e.output.decode('utf-8', errors='ignore').strip()
        return f"Error: {error_out.splitlines()[0] if error_out else 'Command failed'}"
    except Exception as e:
        return f"Error: {str(e)}"

def get_wifi_details(profile):
    try:
        output = subprocess.check_output(
            ['netsh', 'wlan', 'show', 'profile', profile, 'key=clear'], 
            creationflags=subprocess.CREATE_NO_WINDOW,
            stderr=subprocess.STDOUT
        ).decode('utf-8', errors='ignore')
        return output
    except subprocess.CalledProcessError as e:
        return f"Error ({e.returncode}):\n{e.output.decode('utf-8', errors='ignore')}"
    except Exception as e:
        return f"Error fetching details:\n{str(e)}"

def forget_network(profile):
    try:
        subprocess.check_output(
            ['netsh', 'wlan', 'delete', 'profile', f'name={profile}'],
            creationflags=subprocess.CREATE_NO_WINDOW,
            stderr=subprocess.STDOUT
        )
        return True, "Network forgotten successfully."
    except subprocess.CalledProcessError as e:
        return False, e.output.decode('utf-8', errors='ignore')
    except Exception as e:
        return False, str(e)

def toggle_autoconnect(profile, enable):
    try:
        mode = "auto" if enable else "manual"
        subprocess.check_output(
            ['netsh', 'wlan', 'set', 'profileparameter', f'name={profile}', f'connectionmode={mode}'],
            creationflags=subprocess.CREATE_NO_WINDOW,
            stderr=subprocess.STDOUT
        )
        return True, f"Auto-connect set to {mode}."
    except subprocess.CalledProcessError as e:
        return False, e.output.decode('utf-8', errors='ignore')
    except Exception as e:
        return False, str(e)


class NetworkRow(ctk.CTkFrame):
    def __init__(self, master, ssid, password, app_instance, **kwargs):
        super().__init__(master, **kwargs)
        self.ssid = ssid
        self.password = password
        self.app = app_instance
        
        self.is_masked = True
        
        # Determine status icon and color
        if "Open Network" in password:
            icon = "🔓"
            self.pw_display = "Open Network"
            pw_color = "gray"
            self.can_unmask = False
        elif "Error" in password or "Requires Admin" in password or "Unknown" in password:
            icon = "⚠️"
            self.pw_display = password
            pw_color = "orange"
            self.can_unmask = False
        else:
            icon = "🔒"
            self.pw_display = "••••••••"
            pw_color = ["gray10", "gray90"]  # adapts to light/dark
            self.can_unmask = True
            
        self.configure(fg_color=("gray90", "gray13"), corner_radius=8)
        self.pack(fill="x", pady=4, padx=5)
        
        # Info Section (Left)
        info_frame = ctk.CTkFrame(self, fg_color="transparent")
        info_frame.pack(side="left", fill="both", expand=True, padx=10, pady=10)
        
        ssid_label = ctk.CTkLabel(info_frame, text=f"{icon}  {ssid}", font=ctk.CTkFont(size=15, weight="bold"))
        ssid_label.pack(anchor="w")
        
        # Frame for password and eye button
        pw_frame = ctk.CTkFrame(info_frame, fg_color="transparent")
        pw_frame.pack(anchor="w", fill="x")
        
        self.pw_label = ctk.CTkLabel(pw_frame, text=self.pw_display, font=ctk.CTkFont(size=12), text_color=pw_color)
        self.pw_label.pack(side="left")
        
        if self.can_unmask:
            self.unmask_btn = ctk.CTkButton(pw_frame, text="👁️", width=24, height=24, fg_color="transparent", text_color=["gray10", "gray90"], hover_color=("gray80", "gray20"), command=self.toggle_mask)
            self.unmask_btn.pack(side="left", padx=5)

        # Action Buttons (Right) - Touch Friendly
        actions_frame = ctk.CTkFrame(self, fg_color="transparent")
        actions_frame.pack(side="right", padx=10)

        # Helper to create nice small buttons
        def create_btn(text, command, hover_color=None):
            btn = ctk.CTkButton(
                actions_frame, 
                text=text, 
                width=36, 
                height=36, 
                fg_color="transparent", 
                border_width=1,
                text_color=["gray10", "gray90"],
                command=command
            )
            if hover_color:
                btn.configure(hover_color=hover_color)
            return btn

        btn_copy = create_btn("📋", lambda: self.app.copy_to_clipboard(self.password))
        btn_copy.pack(side="left", padx=4)
        
        if QR_AVAILABLE and icon == "🔒":
            btn_qr = create_btn("📱", self.show_qr)
            btn_qr.pack(side="left", padx=4)
            
        btn_details = create_btn("ℹ️", self.show_details)
        btn_details.pack(side="left", padx=4)
        
        btn_auto = create_btn("⚡", self.toggle_auto)
        btn_auto.pack(side="left", padx=4)
        
        btn_forget = create_btn("🗑️", self.forget_this, hover_color="#ff4444")
        btn_forget.pack(side="left", padx=4)

    def toggle_mask(self):
        if self.is_masked:
            self.pw_label.configure(text=self.password)
            self.unmask_btn.configure(text="🙈")
            self.is_masked = False
        else:
            self.pw_label.configure(text="••••••••")
            self.unmask_btn.configure(text="👁️")
            self.is_masked = True

    def show_qr(self):
        self.app.show_qr_code(self.ssid, self.password)
        
    def show_details(self):
        self.app.show_details(self.ssid)
        
    def toggle_auto(self):
        self.app.toggle_auto_gui(self.ssid)
        
    def forget_this(self):
        self.app.forget_network_gui(self.ssid)


class WiFiApp(ctk.CTk):
    def __init__(self):
        super().__init__()

        self.title("WiFi Network Manager")
        self.geometry("800x600")
        self.minsize(600, 400)
        
        self.all_data = []

        # Top Bar
        top_frame = ctk.CTkFrame(self, fg_color="transparent")
        top_frame.pack(fill="x", padx=20, pady=(20, 10))
        
        title_label = ctk.CTkLabel(top_frame, text="Wi-Fi Networks", font=ctk.CTkFont(size=24, weight="bold"))
        title_label.pack(side="left")
        
        self.export_btn = ctk.CTkButton(top_frame, text="Export CSV 💾", command=self.export_csv, width=120)
        self.export_btn.pack(side="right")
        
        self.refresh_btn = ctk.CTkButton(top_frame, text="🔄 Refresh", command=self.start_load_data, width=100, fg_color="transparent", border_width=1, text_color=["gray10", "gray90"])
        self.refresh_btn.pack(side="right", padx=10)

        # Search Bar
        search_frame = ctk.CTkFrame(self, fg_color="transparent")
        search_frame.pack(fill="x", padx=20, pady=(0, 10))
        
        self.search_var = ctk.StringVar()
        self.search_var.trace_add('write', self.filter_list)
        self.search_entry = ctk.CTkEntry(search_frame, placeholder_text="Search SSID... (Ctrl+F)", textvariable=self.search_var, height=35)
        self.search_entry.pack(fill="x", expand=True)

        # Main Scrollable List
        self.scrollable_frame = ctk.CTkScrollableFrame(self, corner_radius=10)
        self.scrollable_frame.pack(fill="both", expand=True, padx=20, pady=(0, 10))

        # Bottom Status/Notification Bar
        self.bottom_frame = ctk.CTkFrame(self, fg_color="transparent", height=30)
        self.bottom_frame.pack(fill="x", padx=20, pady=(0, 10))
        
        self.status_label = ctk.CTkLabel(self.bottom_frame, text="", text_color="green", font=ctk.CTkFont(weight="bold"))
        self.status_label.pack(side="right")
        
        self.loading_label = ctk.CTkLabel(self.bottom_frame, text="", text_color="gray")
        self.loading_label.pack(side="left")

        # Key Bindings
        self.bind("<Control-f>", lambda e: self.search_entry.focus_set())
        
        # Load data
        self.start_load_data()

    def show_status(self, text, is_error=False):
        color = "red" if is_error else "green"
        self.status_label.configure(text=text, text_color=color)
        self.after(3000, lambda: self.status_label.configure(text="")) # Auto-hide after 3 seconds

    def copy_to_clipboard(self, text):
        self.clipboard_clear()
        self.clipboard_append(str(text))
        self.update() 
        self.show_status("Copied to clipboard! 📋")

    def export_csv(self):
        filename = filedialog.asksaveasfilename(
            defaultextension=".csv", 
            filetypes=[("CSV Files", "*.csv"), ("All Files", "*.*")],
            title="Export Wi-Fi Networks"
        )
        if not filename:
            return
            
        try:
            with open(filename, mode='w', newline='', encoding='utf-8') as f:
                writer = csv.writer(f)
                writer.writerow(["SSID", "Password"])
                # Export currently filtered items
                query = self.search_var.get().lower()
                for profile, password in self.all_data:
                    if query in profile.lower():
                        writer.writerow([profile, password])
                        
            self.show_status("Successfully exported to CSV! 💾")
        except Exception as e:
            messagebox.showerror("Error", f"Failed to export: {e}")

    def show_qr_code(self, ssid, pwd):
        qr_data = f"WIFI:T:WPA;S:{ssid};P:{pwd};;"
            
        qr_win = ctk.CTkToplevel(self)
        qr_win.title(f"QR: {ssid}")
        qr_win.geometry("400x450")
        qr_win.attributes("-topmost", True)
        
        try:
            qr = qrcode.QRCode(box_size=10, border=4)
            qr.add_data(qr_data)
            qr.make(fit=True)
            img = qr.make_image(fill_color="black", back_color="white")
            
            tk_image = ImageTk.PhotoImage(img)
            qr_win.tk_image = tk_image # Keep reference
            
            lbl = tk.Label(qr_win, image=tk_image)
            lbl.pack(expand=True, fill="both", padx=20, pady=20)
            
            info = ctk.CTkLabel(qr_win, text="Scan with smartphone camera to connect")
            info.pack(pady=10)
        except Exception as e:
            messagebox.showerror("Error", f"Failed to render QR Code: {e}")

    def show_details(self, ssid):
        details = get_wifi_details(ssid)

        detail_win = ctk.CTkToplevel(self)
        detail_win.title(f"Details for {ssid}")
        detail_win.geometry("550x550")
        
        text_widget = ctk.CTkTextbox(detail_win, wrap="word", font=ctk.CTkFont(family="Consolas", size=12))
        text_widget.pack(fill="both", expand=True, padx=20, pady=20)
        
        text_widget.insert("0.0", details)
        text_widget.configure(state="disabled")

    def forget_network_gui(self, ssid):
        confirm = messagebox.askyesno("Confirm Delete", f"Are you sure you want to forget '{ssid}'?\n\nThis will remove it from Windows completely.")
        if confirm:
            success, msg = forget_network(ssid)
            if success:
                self.show_status(f"Forgot '{ssid}'")
                self.start_load_data()
            else:
                messagebox.showerror("Error", f"Failed to forget network:\n{msg}\n\nNote: You may need Administrator rights.")

    def toggle_auto_gui(self, ssid):
        win = ctk.CTkToplevel(self)
        win.title("Auto-Connect")
        win.geometry("300x150")
        win.attributes("-topmost", True)
        
        ctk.CTkLabel(win, text=f"Auto-Connect for '{ssid}':").pack(pady=10)
        
        def set_mode(enable):
            success, msg = toggle_autoconnect(ssid, enable)
            if success:
                self.show_status(msg)
                win.destroy()
            else:
                messagebox.showerror("Error", f"Failed to update:\n{msg}\n\nYou may need to run this app as Administrator.")
                
        ctk.CTkButton(win, text="Enable Auto-Connect", command=lambda: set_mode(True)).pack(pady=5)
        ctk.CTkButton(win, text="Disable Auto-Connect (Manual)", command=lambda: set_mode(False)).pack(pady=5)

    def start_load_data(self):
        # Clear existing list
        for child in self.scrollable_frame.winfo_children():
            child.destroy()
            
        self.loading_label.configure(text="Fetching networks... please wait.")
        self.refresh_btn.configure(state="disabled")
        
        # Run loading in a thread to prevent UI freezing
        thread = threading.Thread(target=self.load_data)
        thread.daemon = True
        thread.start()

    def load_data(self):
        profiles = get_wifi_profiles()
        
        data = []
        for profile in profiles:
            password = get_wifi_password(profile)
            data.append((profile, password))
            
        # Update UI back in the main thread
        self.after(0, self.on_data_loaded, data)

    def on_data_loaded(self, data):
        self.all_data = data
        self.loading_label.configure(text="")
        self.refresh_btn.configure(state="normal")
        self.filter_list()

    def filter_list(self, *args):
        query = self.search_var.get().lower()
        
        # Clear UI
        for child in self.scrollable_frame.winfo_children():
            child.destroy()
            
        count = 0
        for profile, password in self.all_data:
            if query in profile.lower():
                NetworkRow(self.scrollable_frame, ssid=profile, password=password, app_instance=self)
                count += 1
                
        if count == 0:
            if self.all_data:
                ctk.CTkLabel(self.scrollable_frame, text="No networks match your search.", text_color="gray").pack(pady=20)
            else:
                ctk.CTkLabel(self.scrollable_frame, text="No saved networks found on this system.", text_color="gray").pack(pady=20)

if __name__ == "__main__":
    app = WiFiApp()
    app.mainloop()
