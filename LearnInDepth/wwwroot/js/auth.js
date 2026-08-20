/* ==========================================================================
   LearnInDepth - Authentication Controller (js/auth.js)
   ========================================================================== */

const AuthController = (function() {
  // DOM Elements
  const authModal = document.getElementById('authModal');
  const authModalClose = document.getElementById('authModalClose');
  const authStep1 = document.getElementById('authStep1');
  const authStep2 = document.getElementById('authStep2');
  const authEmailInput = document.getElementById('authEmailInput');
  const authOtpInput = document.getElementById('authOtpInput');
  const sendOtpBtn = document.getElementById('sendOtpBtn');
  const verifyOtpBtn = document.getElementById('verifyOtpBtn');
  const backToEmailBtn = document.getElementById('backToEmailBtn');
  const quickDemoBtn = document.getElementById('quickDemoBtn');

  // Header Elements
  const userProfileMenu = document.getElementById('userProfileMenu');
  const profileBtn = document.getElementById('profileBtn');
  const profileDropdown = document.getElementById('profileDropdown');
  const avatarText = document.getElementById('avatarText');
  const userEmailText = document.getElementById('userEmailText');
  const dropdownEmail = document.getElementById('dropdownEmail');
  const switchAccountBtn = document.getElementById('switchAccountBtn');
  const logoutBtn = document.getElementById('logoutBtn');

  function init() {
    updateUserUI();
    bindEvents();
  }

  function bindEvents() {
    // Profile Dropdown Toggle
    profileBtn.addEventListener('click', (e) => {
      e.stopPropagation();
      profileDropdown.classList.toggle('hidden');
    });

    document.addEventListener('click', () => {
      profileDropdown.classList.add('hidden');
    });

    profileDropdown.addEventListener('click', (e) => {
      e.stopPropagation();
    });

    // Modal Close
    authModalClose.addEventListener('click', hideModal);

    // Switch Account
    switchAccountBtn.addEventListener('click', () => {
      profileDropdown.classList.add('hidden');
      showModal();
    });

    // Logout
    logoutBtn.addEventListener('click', () => {
      ApiClient.clearSession();
      updateUserUI();
      profileDropdown.classList.add('hidden');
      App.showToast('Logged out successfully', 'info');
      App.navigate('dashboard');
    });

    // Step 1: Send OTP
    sendOtpBtn.addEventListener('click', async () => {
      const email = authEmailInput.value.trim();
      if (!email || !email.includes('@')) {
        App.showToast('Please enter a valid email address', 'error');
        return;
      }

      sendOtpBtn.disabled = true;
      sendOtpBtn.innerText = 'Sending OTP...';

      try {
        const res = await ApiClient.sendOtp(email);
        App.showToast(res.message || 'OTP sent to your email address', 'success');
        authStep1.classList.add('hidden');
        authStep2.classList.remove('hidden');
        authOtpInput.focus();
      } catch (err) {
        App.showToast(err.message || 'Failed to send OTP', 'error');
      } finally {
        sendOtpBtn.disabled = false;
        sendOtpBtn.innerText = 'Send Verification OTP';
      }
    });

    // Back to email step
    backToEmailBtn.addEventListener('click', () => {
      authStep2.classList.add('hidden');
      authStep1.classList.remove('hidden');
    });

    // Step 2: Verify OTP
    verifyOtpBtn.addEventListener('click', async () => {
      const email = authEmailInput.value.trim();
      const otp = authOtpInput.value.trim();

      if (!otp) {
        App.showToast('Please enter the verification OTP code', 'error');
        return;
      }

      verifyOtpBtn.disabled = true;
      verifyOtpBtn.innerText = 'Verifying...';

      try {
        const res = await ApiClient.verifyOtp(email, otp);
        ApiClient.setSession(res.authToken, res.email);
        updateUserUI();
        hideModal();
        App.showToast(`Signed in as ${res.email}`, 'success');
        App.refreshDashboard();
      } catch (err) {
        App.showToast(err.message || 'Invalid OTP code', 'error');
      } finally {
        verifyOtpBtn.disabled = false;
        verifyOtpBtn.innerText = 'Verify & Sign In';
      }
    });

    // Quick Demo Mode
    quickDemoBtn.addEventListener('click', () => {
      const demoEmail = 'demo.engineer@learnindepth.com';
      const demoToken = 'demo-jwt-token-access-granted';
      ApiClient.setSession(demoToken, demoEmail);
      updateUserUI();
      hideModal();
      App.showToast('Instant Quick Access activated (Demo Mode)', 'success');
      App.refreshDashboard();
    });

    // Listen for unauthorized 401 events
    window.addEventListener('unauthorized_event', () => {
      showModal();
    });
  }

  function showModal() {
    authStep1.classList.remove('hidden');
    authStep2.classList.add('hidden');
    authModal.classList.remove('hidden');
    authEmailInput.focus();
  }

  function hideModal() {
    authModal.classList.add('hidden');
  }

  function updateUserUI() {
    const email = ApiClient.getEmail();
    const token = ApiClient.getToken();

    if (email && email !== 'guest@learnindepth.local') {
      userEmailText.innerText = email;
      dropdownEmail.innerText = email;
      avatarText.innerText = email.charAt(0).toUpperCase();
    } else {
      userEmailText.innerText = 'Guest User';
      dropdownEmail.innerText = 'guest@learnindepth.local';
      avatarText.innerText = 'G';
    }
  }

  return {
    init,
    showModal,
    hideModal,
    updateUserUI
  };
})();
