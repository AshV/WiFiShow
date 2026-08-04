let allNetworks = [];
let isAllMasked = true;

const icons = {
  lock: '<svg class="icon-svg" viewBox="0 0 24 24"><rect x="3" y="11" width="18" height="11" rx="2" ry="2"></rect><path d="M7 11V7a5 5 0 0110 0v4"></path></svg>',
  unlock: '<svg class="icon-svg" viewBox="0 0 24 24"><rect x="3" y="11" width="18" height="11" rx="2" ry="2"></rect><path d="M7 11V7a5 5 0 019.9-1"></path></svg>',
  warning: '<svg class="icon-svg" viewBox="0 0 24 24"><path d="M10.29 3.86L1.82 18a2 2 0 001.71 3h16.94a2 2 0 001.71-3L13.71 3.86a2 2 0 00-3.42 0z"></path><line x1="12" y1="9" x2="12" y2="13"></line><line x1="12" y1="17" x2="12.01" y2="17"></line></svg>',
  eye: '<svg class="icon-svg" viewBox="0 0 24 24"><path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z"></path><circle cx="12" cy="12" r="3"></circle></svg>',
  eyeOff: '<svg class="icon-svg" viewBox="0 0 24 24"><path d="M17.94 17.94A10.07 10.07 0 0112 20c-7 0-11-8-11-8a18.45 18.45 0 015.06-5.94M9.9 4.24A9.12 9.12 0 0112 4c7 0 11 8 11 8a18.5 18.5 0 01-2.16 3.19m-6.72-1.07a3 3 0 11-4.24-4.24"></path><line x1="1" y1="1" x2="23" y2="23"></line></svg>',
  copy: '<svg class="icon-svg" viewBox="0 0 24 24"><rect x="9" y="9" width="13" height="13" rx="2" ry="2"></rect><path d="M5 15H4a2 2 0 01-2-2V4a2 2 0 012-2h9a2 2 0 012 2v1"></path></svg>',
  info: '<svg class="icon-svg" viewBox="0 0 24 24"><circle cx="12" cy="12" r="10"></circle><line x1="12" y1="16" x2="12" y2="12"></line><line x1="12" y1="8" x2="12.01" y2="8"></line></svg>',
  trash: '<svg class="icon-svg" viewBox="0 0 24 24"><polyline points="3 6 5 6 21 6"></polyline><path d="M19 6v14a2 2 0 01-2 2H7a2 2 0 01-2-2V6m3 0V4a2 2 0 012-2h4a2 2 0 012 2v2"></path><line x1="10" y1="11" x2="10" y2="17"></line><line x1="14" y1="11" x2="14" y2="17"></line></svg>',
  qr: '<svg class="icon-svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><rect width="5" height="5" x="3" y="3" rx="1"/><rect width="5" height="5" x="16" y="3" rx="1"/><rect width="5" height="5" x="3" y="16" rx="1"/><path d="M21 16h-3a2 2 0 0 0-2 2v3"/><path d="M21 21v.01"/><path d="M12 7v3a2 2 0 0 1-2 2H7"/><path d="M3 12h.01"/><path d="M12 3h.01"/><path d="M12 16v.01"/><path d="M16 12h1"/><path d="M21 12v.01"/><path d="M12 21v-1"/></svg>'
};

Neutralino.init();

Neutralino.events.on("windowClose", () => {
    Neutralino.app.exit();
});

document.addEventListener('DOMContentLoaded', () => {
  document.getElementById('refreshBtn').addEventListener('click', loadNetworks);
  document.getElementById('searchInput').addEventListener('input', filterNetworks);
  document.getElementById('closeModalIconBtn').addEventListener('click', closeModal);
  document.getElementById('exportBtn').addEventListener('click', exportNetworks);
  
  document.getElementById('toggleAllBtn').addEventListener('click', (e) => {
    isAllMasked = !isAllMasked;
    const btn = e.currentTarget;
    btn.innerHTML = `
      ${isAllMasked ? icons.eye : icons.eyeOff}
      <span>${isAllMasked ? "Show All" : "Hide All"}</span>
    `;
    allNetworks.forEach(net => {
      net.isMasked = isAllMasked;
    });
    filterNetworks(); // Re-render to update
  });

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

async function loadNetworks() {
  const loading = document.getElementById('loading');
  const list = document.getElementById('networkList');
  
  loading.classList.remove('hidden');
  list.classList.add('hidden');
  list.innerHTML = '';
  allNetworks = [];

  try {
    const tempDir = await Neutralino.os.getEnv("TEMP");
    const exportFolder = `${tempDir}\\wifi-export-neu`;
    
    // Create folder if not exists
    await runCmd(`powershell -Command "New-Item -ItemType Directory -Force -Path '${exportFolder}'"`);
    
    // Export all profiles as XML
    await runCmd(`netsh wlan export profile folder="${exportFolder}" key=clear`);
    
    // Read the directory
    const files = await Neutralino.filesystem.readDirectory(exportFolder);
    
    for (let file of files) {
      if (file.type === 'FILE' && file.entry.endsWith('.xml')) {
         const xmlContent = await Neutralino.filesystem.readFile(`${exportFolder}\\${file.entry}`);
         
         const parser = new DOMParser();
         const xmlDoc = parser.parseFromString(xmlContent, "text/xml");
         
         const getTag = (tag) => {
             const el = xmlDoc.getElementsByTagName(tag)[0];
             return el ? el.textContent : "";
         };
         
         const name = getTag("name");
         const connectionMode = getTag("connectionMode"); // auto or manual
         const authentication = getTag("authentication"); // WPA2PSK, open, etc.
         const keyMaterial = getTag("keyMaterial"); // password
         
         let password = keyMaterial;
         let authType = authentication;
         let isAuto = (connectionMode === "auto");
         
         let finalPassword = password;
         if (!password) {
             if (authType.toLowerCase().includes("open")) finalPassword = "Open Network (No Password)";
             else if (authType.toLowerCase().includes("enterprise")) finalPassword = "Enterprise Network (Username/Cert)";
             else finalPassword = "Password Not Saved";
         }
         
         allNetworks.push({
             profile: name,
             details: {
                password: finalPassword,
                isAuto: isAuto,
                authType: authType,
                hasRealPassword: !!password
             },
             isMasked: isAllMasked
         });
      }
    }
    
    // Cleanup temp directory
    try {
        await runCmd(`powershell -Command "Remove-Item -Recurse -Force '${exportFolder}'"`);
    } catch(e) {}
    
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
    let pwdDisplay = "••••••••";
    let canUnmask = true; 
    
    if (net.details) {
      if (net.details.password.includes("Open Network")) {
        icon = icons.unlock;
        pwdDisplay = "Open Network";
      } else if (!net.details.hasRealPassword) {
        icon = icons.warning;
        pwdDisplay = net.details.password;
      } else {
        pwdDisplay = net.isMasked ? "••••••••" : net.details.password;
      }
      canUnmask = net.details.hasRealPassword;
    }

    const card = document.createElement('div');
    card.className = 'network-card';

    // Header
    const header = document.createElement('div');
    header.className = 'card-header';
    const ssid = document.createElement('div');
    ssid.className = 'card-ssid';
    ssid.innerHTML = `<span>${icon}</span> <span>${net.profile}</span>`;
    header.appendChild(ssid);

    // Body
    const body = document.createElement('div');
    body.className = 'card-body';
    
    const pwdContainer = document.createElement('div');
    pwdContainer.className = 'pwd-container';
    
    const pwdLabel = document.createElement('span');
    pwdLabel.className = 'pwd-text';
    pwdLabel.textContent = pwdDisplay;
    pwdContainer.appendChild(pwdLabel);

    if (canUnmask) {
      const unmaskBtn = document.createElement('button');
      unmaskBtn.className = 'icon-btn';
      unmaskBtn.innerHTML = net.isMasked ? icons.eye : icons.eyeOff;
      unmaskBtn.onclick = async () => {
        net.isMasked = !net.isMasked;
        filterNetworks(); 
      };
      pwdContainer.appendChild(unmaskBtn);
    }
    body.appendChild(pwdContainer);

    // Footer
    const footer = document.createElement('div');
    footer.className = 'card-footer';
    
    const actions = document.createElement('div');
    actions.className = 'card-actions';

    // QR Code Button
    if (net.details && net.details.hasRealPassword) {
      const qrBtn = document.createElement('button');
      qrBtn.className = 'icon-btn has-tooltip';
      qrBtn.setAttribute('data-tooltip', 'Show QR Code');
      qrBtn.innerHTML = icons.qr;
      qrBtn.onclick = async () => {
        const encryption = net.details.authType.toLowerCase().includes("wep") ? "WEP" : "WPA";
        const qrString = `WIFI:S:${net.profile};T:${encryption};P:${net.details.password};;`;
        showModal(`QR Code for ${net.profile}`, null, qrString);
      };
      actions.appendChild(qrBtn);
    }

    // Copy Button
    if (net.details && net.details.hasRealPassword) {
        const copyBtn = document.createElement('button');
        copyBtn.className = 'icon-btn has-tooltip';
        copyBtn.setAttribute('data-tooltip', 'Copy Password');
        copyBtn.innerHTML = icons.copy;
        copyBtn.onclick = async () => {
          try {
            await Neutralino.clipboard.writeText(net.details.password);
            showToast('Copied to clipboard! 📋');
          } catch (e) {
            showToast('Failed to copy');
          }
        };
        actions.appendChild(copyBtn);
    }

    const detailsBtn = document.createElement('button');
    detailsBtn.className = 'icon-btn has-tooltip';
    detailsBtn.setAttribute('data-tooltip', 'Details');
    detailsBtn.innerHTML = icons.info;
    detailsBtn.onclick = async () => {
      try {
        const detailsText = await runCmd(`netsh wlan show profile name="${net.profile}" key=clear`);
        showModal(`Details for ${net.profile}`, detailsText);
      } catch (e) {
        showToast('Error fetching details');
      }
    };
    actions.appendChild(detailsBtn);

      const sep = document.createElement('div');
      sep.style.width = '1px';
      sep.style.height = '16px';
      sep.style.background = 'var(--sep-color)';
    sep.style.margin = '0 0.5rem';
    actions.appendChild(sep);

    // Auto-Connect Toggle
    const autoToggle = document.createElement('label');
    autoToggle.className = 'toggle-switch has-tooltip';
    autoToggle.setAttribute('data-tooltip', 'Auto-connect');
    const autoInput = document.createElement('input');
    autoInput.type = 'checkbox';
    if (net.details) {
        autoInput.checked = net.details.isAuto;
    }
    const autoSlider = document.createElement('span');
    autoSlider.className = 'slider';
    
    autoInput.onchange = async (e) => {
      const isChecked = e.target.checked;
      const mode = isChecked ? "auto" : "manual";
      try {
        await runCmd(`netsh wlan set profileparameter name="${net.profile}" connectionmode=${mode}`);
        net.details.isAuto = isChecked;
        showToast(`Auto-connect ${isChecked ? 'enabled' : 'disabled'}.`);
      } catch (err) {
        showToast(`Error updating mode`);
        e.target.checked = !isChecked; // revert on fail
      }
    };
    autoToggle.appendChild(autoInput);
    autoToggle.appendChild(autoSlider);
    actions.appendChild(autoToggle);

    const sep2 = document.createElement('div');
    sep2.style.width = '1px';
    sep2.style.height = '18px';
    sep2.style.background = 'rgba(255,255,255,0.1)';
    sep2.style.margin = '0 0.5rem';
    actions.appendChild(sep2);

    const forgetBtn = document.createElement('button');
    forgetBtn.className = 'icon-btn danger has-tooltip';
    forgetBtn.setAttribute('data-tooltip', 'Forget Network');
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
    actions.appendChild(forgetBtn);

    footer.appendChild(actions);

    card.appendChild(header);
    card.appendChild(body);
    card.appendChild(footer);
    
    list.appendChild(card);
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

async function exportNetworks() {
    let csv = "SSID,Password,AuthType,AutoConnect\n";
    allNetworks.forEach(n => {
        let p = "";
        if (n.details && n.details.hasRealPassword) p = n.details.password;
        csv += `"${n.profile}","${p}","${n.details ? n.details.authType : ''}","${n.details ? n.details.isAuto : ''}"\n`;
    });

    try {
        let savePath = await Neutralino.os.showSaveDialog('Save Export', {
            defaultPath: 'wifi-export.csv',
            filters: [{name: 'CSV Files', extensions: ['csv']}]
        });
        if (savePath) {
            await Neutralino.filesystem.writeFile(savePath, csv);
            showToast("Export saved!");
        } else {
            showToast("Export cancelled.");
        }
    } catch(e) {
        showToast(`Export failed: ${e}`);
    }
}
