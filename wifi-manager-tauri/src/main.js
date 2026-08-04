import { invoke } from '@tauri-apps/api/core';

let allNetworks = [];

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

async function loadNetworks() {
  const loading = document.getElementById('loading');
  const list = document.getElementById('networkList');
  
  loading.classList.remove('hidden');
  list.classList.add('hidden');
  list.innerHTML = '';
  allNetworks = [];

  try {
    const profiles = await invoke('get_wifi_profiles');
    
    for (const profile of profiles) {
      const password = await invoke('get_wifi_password', { profile });
      allNetworks.push({ profile, password });
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
    let icon = "🔒";
    let isMasked = true;
    let canUnmask = true;
    let pwdDisplay = "••••••••";
    
    if (net.password.includes("Open Network")) {
      icon = "🔓";
      pwdDisplay = "Open Network";
      canUnmask = false;
    } else if (net.password.includes("Error") || net.password.includes("Requires Admin") || net.password.includes("Unknown")) {
      icon = "⚠️";
      pwdDisplay = net.password;
      canUnmask = false;
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
      unmaskBtn.textContent = '👁️';
      unmaskBtn.onclick = () => {
        if (isMasked) {
          pwdLabel.textContent = net.password;
          unmaskBtn.textContent = '🙈';
        } else {
          pwdLabel.textContent = '••••••••';
          unmaskBtn.textContent = '👁️';
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

    const copyBtn = document.createElement('button');
    copyBtn.className = 'icon-btn';
    copyBtn.title = 'Copy Password';
    copyBtn.textContent = '📋';
    copyBtn.onclick = () => {
      navigator.clipboard.writeText(net.password);
      showToast('Copied to clipboard! 📋');
    };

    const detailsBtn = document.createElement('button');
    detailsBtn.className = 'icon-btn';
    detailsBtn.title = 'Details';
    detailsBtn.textContent = 'ℹ️';
    detailsBtn.onclick = async () => {
      try {
        const details = await invoke('get_wifi_details', { profile: net.profile });
        showModal(`Details for ${net.profile}`, details);
      } catch (e) {
        showToast('Error fetching details');
      }
    };

    const autoBtn = document.createElement('button');
    autoBtn.className = 'icon-btn';
    autoBtn.title = 'Enable Auto-Connect';
    autoBtn.textContent = '⚡';
    autoBtn.onclick = async () => {
      try {
        const res = await invoke('toggle_autoconnect', { profile: net.profile, enable: true });
        showToast(res);
      } catch (e) {
        showToast(`Error: ${e}`);
      }
    };

    const forgetBtn = document.createElement('button');
    forgetBtn.className = 'icon-btn danger';
    forgetBtn.title = 'Forget Network';
    forgetBtn.textContent = '🗑️';
    forgetBtn.onclick = async () => {
      if (confirm(`Are you sure you want to forget '${net.profile}'?`)) {
        try {
          const res = await invoke('forget_network', { profile: net.profile });
          showToast(res);
          loadNetworks();
        } catch (e) {
          showToast(`Error: ${e}`);
        }
      }
    };

    actions.appendChild(copyBtn);
    actions.appendChild(detailsBtn);
    actions.appendChild(autoBtn);
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

function showModal(title, text) {
  document.getElementById('modalTitle').textContent = title;
  document.getElementById('modalText').textContent = text;
  document.getElementById('detailsModal').classList.remove('hidden');
}

function closeModal() {
  document.getElementById('detailsModal').classList.add('hidden');
}
