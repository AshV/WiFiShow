let allNetworks = [];

const icons = {
  lock: '<svg class="icon-svg" viewBox="0 0 24 24"><rect x="3" y="11" width="18" height="11" rx="2" ry="2"></rect><path d="M7 11V7a5 5 0 0110 0v4"></path></svg>',
  unlock: '<svg class="icon-svg" viewBox="0 0 24 24"><rect x="3" y="11" width="18" height="11" rx="2" ry="2"></rect><path d="M7 11V7a5 5 0 019.9-1"></path></svg>',
  warning: '<svg class="icon-svg" viewBox="0 0 24 24"><path d="M10.29 3.86L1.82 18a2 2 0 001.71 3h16.94a2 2 0 001.71-3L13.71 3.86a2 2 0 00-3.42 0z"></path><line x1="12" y1="9" x2="12" y2="13"></line><line x1="12" y1="17" x2="12.01" y2="17"></line></svg>',
  eye: '<svg class="icon-svg" viewBox="0 0 24 24"><path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z"></path><circle cx="12" cy="12" r="3"></circle></svg>',
  eyeOff: '<svg class="icon-svg" viewBox="0 0 24 24"><path d="M17.94 17.94A10.07 10.07 0 0112 20c-7 0-11-8-11-8a18.45 18.45 0 015.06-5.94M9.9 4.24A9.12 9.12 0 0112 4c7 0 11 8 11 8a18.5 18.5 0 01-2.16 3.19m-6.72-1.07a3 3 0 11-4.24-4.24"></path><line x1="1" y1="1" x2="23" y2="23"></line></svg>',
  copy: '<svg class="icon-svg" viewBox="0 0 24 24"><rect x="9" y="9" width="13" height="13" rx="2" ry="2"></rect><path d="M5 15H4a2 2 0 01-2-2V4a2 2 0 012-2h9a2 2 0 012 2v1"></path></svg>',
  info: '<svg class="icon-svg" viewBox="0 0 24 24"><circle cx="12" cy="12" r="10"></circle><line x1="12" y1="16" x2="12" y2="12"></line><line x1="12" y1="8" x2="12.01" y2="8"></line></svg>',
  trash: '<svg class="icon-svg" viewBox="0 0 24 24"><polyline points="3 6 5 6 21 6"></polyline><path d="M19 6v14a2 2 0 01-2 2H7a2 2 0 01-2-2V6m3 0V4a2 2 0 012-2h4a2 2 0 012 2v2"></path><line x1="10" y1="11" x2="10" y2="17"></line><line x1="14" y1="11" x2="14" y2="17"></line></svg>',
  qr: '<svg class="icon-svg" viewBox="0 0 24 24"><rect x="3" y="3" width="7" height="7"></rect><rect x="14" y="3" width="7" height="7"></rect><rect x="14" y="14" width="7" height="7"></rect><rect x="3" y="14" width="7" height="7"></rect></svg>'
};

Neutralino.init();

Neutralino.events.on("windowClose", () => {
    Neutralino.app.exit();
});

document.addEventListener('DOMContentLoaded', () => {
  document.getElementById('refreshBtn').addEventListener('click', loadNetworks);
  document.getElementById('searchInput').addEventListener('input', filterNetworks);
  document.getElementById('closeModalBtn').addEventListener('click', closeModal);
  
  // Shortcut Ctrl+F
  document.addEventListener('keydown', (e) => {
    if (e.ctrlKey && e.key === 'f') {
      e.preventDefault();
      document.getElementById('searchInput').focus();
    }
  });

  loadNetworks();
});

async function runCmd(cmd) {
    let result = await Neutralino.os.execCommand(cmd);
    if (result.exitCode !== 0) {
        throw new Error(result.stdErr || "Command failed");
    }
    return result.stdOut;
}

async function getWifiProfiles() {
    const stdout = await runCmd('netsh wlan show profiles');
    const profiles = [];
    const lines = stdout.split('\n');
    for (let line of lines) {
        if (line.includes('All User Profile')) {
            const parts = line.split(':');
            if (parts.length > 1) {
                profiles.push(parts[1].trim());
            }
        }
    }
    return profiles;
}

async function getWifiDetails(profile) {
    try {
        const stdout = await runCmd(`netsh wlan show profile name="${profile}" key=clear`);
        
        let password = "";
        let authType = "";
        let securityKey = "";
        let isAuto = false;

        const lines = stdout.split('\n');
        for (let line of lines) {
            const trimmed = line.trim();
            if (trimmed.startsWith("Authentication")) {
                authType = trimmed.split(':')[1].trim();
            } else if (trimmed.startsWith("Security key")) {
                securityKey = trimmed.split(':')[1].trim();
            } else if (trimmed.startsWith("Key Content")) {
                password = trimmed.split(':')[1].trim();
            } else if (trimmed.startsWith("Connection mode")) {
                isAuto = trimmed.split(':')[1].trim() === "Connect automatically";
            }
        }

        let finalPassword = password;
        if (!password) {
            if (authType.includes("Open")) finalPassword = "Open Network (No Password)";
            else if (authType.includes("Enterprise")) finalPassword = "Enterprise Network (Username/Cert)";
            else if (securityKey === "Present") finalPassword = "Requires Admin Rights";
            else if (securityKey === "Absent") finalPassword = "Password Not Saved";
            else finalPassword = "Unknown Status";
        }
        
        return {
            password: finalPassword,
            isAuto: isAuto,
            authType: authType,
            hasRealPassword: !!password
        };
    } catch (e) {
        return {
            password: "Error reading password",
            isAuto: false,
            authType: "",
            hasRealPassword: false
        };
    }
}

async function loadNetworks() {
  const loading = document.getElementById('loading');
  const list = document.getElementById('networkList');
  
  loading.classList.remove('hidden');
  list.classList.add('hidden');
  list.innerHTML = '';
  allNetworks = [];

  try {
    const profiles = await getWifiProfiles();
    
    for (const profile of profiles) {
      const details = await getWifiDetails(profile);
      allNetworks.push({ profile, ...details });
    }
    
    renderNetworks(allNetworks);
  } catch (error) {
    showToast(`Error: ${error}`);
  } finally {
    loading.classList.add('hidden');
    list.classList.remove('hidden');
  }
}

function renderNetworks(networks) {
  const list = document.getElementById('networkList');
  list.innerHTML = '';
  
  if (networks.length === 0) {
    list.innerHTML = '<div style="text-align: center; color: var(--text-muted); padding: 2rem;">No networks found.</div>';
    return;
  }

  networks.forEach(net => {
    let icon = icons.lock;
    let isMasked = true;
    let canUnmask = net.hasRealPassword;
    let pwdDisplay = "••••••••";
    
    if (net.password.includes("Open Network")) {
      icon = icons.unlock;
      pwdDisplay = "Open Network";
    } else if (!net.hasRealPassword) {
      icon = icons.warning;
      pwdDisplay = net.password;
    }

    const row = document.createElement('div');
    row.className = 'network-row';

    // Left Info
    const info = document.createElement('div');
    info.className = 'row-info';
    
    const ssid = document.createElement('div');
    ssid.className = 'row-ssid';
    ssid.innerHTML = `<span>${icon}</span> <span>${net.profile}</span>`;
    
    const pwdContainer = document.createElement('div');
    pwdContainer.className = 'row-pwd-container';
    
    const pwdLabel = document.createElement('span');
    pwdLabel.className = 'row-pwd';
    pwdLabel.textContent = pwdDisplay;
    
    pwdContainer.appendChild(pwdLabel);

    if (canUnmask) {
      const unmaskBtn = document.createElement('button');
      unmaskBtn.className = 'icon-btn';
      unmaskBtn.innerHTML = icons.eye;
      unmaskBtn.onclick = () => {
        if (isMasked) {
          pwdLabel.textContent = net.password;
          unmaskBtn.innerHTML = icons.eyeOff;
        } else {
          pwdLabel.textContent = '••••••••';
          unmaskBtn.innerHTML = icons.eye;
        }
        isMasked = !isMasked;
      };
      pwdContainer.appendChild(unmaskBtn);
    }
    
    info.appendChild(ssid);
    info.appendChild(pwdContainer);

    // Right Actions
    const actions = document.createElement('div');
    actions.className = 'row-actions';

    // Auto-Connect Toggle
    const autoLabel = document.createElement('span');
    autoLabel.className = 'action-label';
    autoLabel.textContent = 'Auto-connect';

    const autoToggle = document.createElement('label');
    autoToggle.className = 'toggle-switch';
    const autoInput = document.createElement('input');
    autoInput.type = 'checkbox';
    autoInput.checked = net.isAuto;
    const autoSlider = document.createElement('span');
    autoSlider.className = 'slider';
    
    autoInput.onchange = async (e) => {
      const isChecked = e.target.checked;
      const mode = isChecked ? "auto" : "manual";
      try {
        await runCmd(`netsh wlan set profileparameter name="${net.profile}" connectionmode=${mode}`);
        showToast(`Auto-connect ${isChecked ? 'enabled' : 'disabled'}.`);
      } catch (err) {
        showToast(`Error updating mode`);
        e.target.checked = !isChecked; // revert on fail
      }
    };
    autoToggle.appendChild(autoInput);
    autoToggle.appendChild(autoSlider);

    // Separator
    const sep = document.createElement('div');
    sep.style.width = '1px';
    sep.style.height = '20px';
    sep.style.background = 'rgba(255,255,255,0.1)';
    sep.style.margin = '0 0.5rem';

    // QR Code Button
    if (net.hasRealPassword) {
      const qrBtn = document.createElement('button');
      qrBtn.className = 'icon-btn';
      qrBtn.title = 'Show QR Code';
      qrBtn.innerHTML = icons.qr;
      qrBtn.onclick = () => {
        // WIFI:S:<SSID>;T:<WEP|WPA|blank>;P:<PASSWORD>;H:<true|false|blank>;;
        const encryption = net.authType.includes("WEP") ? "WEP" : "WPA";
        const qrString = `WIFI:S:${net.profile};T:${encryption};P:${net.password};;`;
        
        showModal(`QR Code for ${net.profile}`, null, qrString);
      };
      actions.appendChild(qrBtn);
    }

    const copyBtn = document.createElement('button');
    copyBtn.className = 'icon-btn';
    copyBtn.title = 'Copy Password';
    copyBtn.innerHTML = icons.copy;
    copyBtn.onclick = async () => {
      try {
        await Neutralino.clipboard.writeText(net.password);
        showToast('Copied to clipboard! 📋');
      } catch (e) {
        showToast('Failed to copy');
      }
    };

    const detailsBtn = document.createElement('button');
    detailsBtn.className = 'icon-btn';
    detailsBtn.title = 'Details';
    detailsBtn.innerHTML = icons.info;
    detailsBtn.onclick = async () => {
      try {
        const details = await runCmd(`netsh wlan show profile name="${net.profile}" key=clear`);
        showModal(`Details for ${net.profile}`, details);
      } catch (e) {
        showToast('Error fetching details');
      }
    };

    const forgetBtn = document.createElement('button');
    forgetBtn.className = 'icon-btn danger';
    forgetBtn.title = 'Forget Network';
    forgetBtn.innerHTML = icons.trash;
    forgetBtn.onclick = async () => {
      if (confirm(`Are you sure you want to forget '${net.profile}'?`)) {
        try {
          await runCmd(`netsh wlan delete profile name="${net.profile}"`);
          showToast("Network forgotten successfully.");
          loadNetworks();
        } catch (e) {
          showToast(`Error: ${e}`);
        }
      }
    };

    actions.appendChild(autoLabel);
    actions.appendChild(autoToggle);
    actions.appendChild(sep);
    if (!net.hasRealPassword) actions.appendChild(copyBtn); // already added if hasRealPassword? wait, I didn't add it yet
    if (net.hasRealPassword) actions.appendChild(copyBtn);
    actions.appendChild(detailsBtn);
    actions.appendChild(forgetBtn);

    row.appendChild(info);
    row.appendChild(actions);
    list.appendChild(row);
  });
}

function filterNetworks() {
  const query = document.getElementById('searchInput').value.toLowerCase();
  const filtered = allNetworks.filter(net => net.profile.toLowerCase().includes(query));
  renderNetworks(filtered);
}

function showToast(msg) {
  const toast = document.getElementById('toast');
  toast.textContent = msg;
  toast.classList.add('show');
  setTimeout(() => toast.classList.remove('show'), 3000);
}

function showModal(title, text, qrData = null) {
  document.getElementById('modalTitle').textContent = title;
  
  const textContainer = document.getElementById('modalText');
  const qrContainer = document.getElementById('qrcode');
  
  if (text) {
    textContainer.style.display = 'block';
    textContainer.textContent = text;
    qrContainer.style.display = 'none';
    qrContainer.innerHTML = '';
  } else if (qrData) {
    textContainer.style.display = 'none';
    qrContainer.style.display = 'block';
    qrContainer.innerHTML = '';
    new QRCode(qrContainer, {
        text: qrData,
        width: 200,
        height: 200,
        colorDark : "#000000",
        colorLight : "#ffffff",
        correctLevel : QRCode.CorrectLevel.M
    });
  }
  
  document.getElementById('detailsModal').classList.remove('hidden');
}

function closeModal() {
  document.getElementById('detailsModal').classList.add('hidden');
}
