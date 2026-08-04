// Prevents additional console window on Windows in release, DO NOT REMOVE!!
#![cfg_attr(not(debug_assertions), windows_subsystem = "windows")]

use std::process::Command;
use std::os::windows::process::CommandExt;

const CREATE_NO_WINDOW: u32 = 0x08000000;

#[tauri::command]
fn get_wifi_profiles() -> Result<Vec<String>, String> {
    let output = Command::new("netsh")
        .args(["wlan", "show", "profiles"])
        .creation_flags(CREATE_NO_WINDOW)
        .output()
        .map_err(|e| e.to_string())?;

    let stdout = String::from_utf8_lossy(&output.stdout);
    let mut profiles = Vec::new();

    for line in stdout.lines() {
        if line.contains("All User Profile") {
            if let Some(parts) = line.split_once(':') {
                profiles.push(parts.1.trim().to_string());
            }
        }
    }

    Ok(profiles)
}

#[tauri::command]
fn get_wifi_password(profile: String) -> Result<String, String> {
    let output = Command::new("netsh")
        .args(["wlan", "show", "profile", &profile, "key=clear"])
        .creation_flags(CREATE_NO_WINDOW)
        .output()
        .map_err(|e| e.to_string())?;

    let stdout = String::from_utf8_lossy(&output.stdout);
    
    let mut password = String::new();
    let mut auth_type = String::new();
    let mut security_key = String::new();

    for line in stdout.lines() {
        let trimmed = line.trim();
        if trimmed.starts_with("Authentication") {
            if let Some(parts) = trimmed.split_once(':') {
                auth_type = parts.1.trim().to_string();
            }
        } else if trimmed.starts_with("Security key") {
            if let Some(parts) = trimmed.split_once(':') {
                security_key = parts.1.trim().to_string();
            }
        } else if trimmed.starts_with("Key Content") {
            if let Some(parts) = trimmed.split_once(':') {
                password = parts.1.trim().to_string();
                break;
            }
        }
    }

    if !password.is_empty() {
        return Ok(password);
    }

    if auth_type.contains("Open") {
        return Ok("Open Network (No Password)".to_string());
    } else if auth_type.contains("Enterprise") {
        return Ok("Enterprise Network (Username/Cert)".to_string());
    } else if security_key == "Present" {
        return Ok("Requires Admin Rights".to_string());
    } else if security_key == "Absent" {
        return Ok("Password Not Saved".to_string());
    }

    for line in stdout.lines() {
        let trimmed = line.trim();
        if !trimmed.is_empty() && !trimmed.starts_with("Profile") && !trimmed.starts_with("=====") {
            return Ok(format!("Detail: {}", trimmed));
        }
    }

    Ok("Unknown Status".to_string())
}

#[tauri::command]
fn get_wifi_details(profile: String) -> Result<String, String> {
    let output = Command::new("netsh")
        .args(["wlan", "show", "profile", &profile, "key=clear"])
        .creation_flags(CREATE_NO_WINDOW)
        .output()
        .map_err(|e| e.to_string())?;

    let stdout = String::from_utf8_lossy(&output.stdout);
    Ok(stdout.to_string())
}

#[tauri::command]
fn forget_network(profile: String) -> Result<String, String> {
    let output = Command::new("netsh")
        .args(["wlan", "delete", "profile", &format!("name={}", profile)])
        .creation_flags(CREATE_NO_WINDOW)
        .output()
        .map_err(|e| e.to_string())?;

    if output.status.success() {
        Ok("Network forgotten successfully.".to_string())
    } else {
        Err(String::from_utf8_lossy(&output.stderr).to_string())
    }
}

#[tauri::command]
fn toggle_autoconnect(profile: String, enable: bool) -> Result<String, String> {
    let mode = if enable { "auto" } else { "manual" };
    let output = Command::new("netsh")
        .args(["wlan", "set", "profileparameter", &format!("name={}", profile), &format!("connectionmode={}", mode)])
        .creation_flags(CREATE_NO_WINDOW)
        .output()
        .map_err(|e| e.to_string())?;

    if output.status.success() {
        Ok(format!("Auto-connect set to {}.", mode))
    } else {
        Err(String::from_utf8_lossy(&output.stderr).to_string())
    }
}

fn main() {
    tauri::Builder::default()
        .invoke_handler(tauri::generate_handler![
            get_wifi_profiles,
            get_wifi_password,
            get_wifi_details,
            forget_network,
            toggle_autoconnect
        ])
        .run(tauri::generate_context!())
        .expect("error while running tauri application");
}
