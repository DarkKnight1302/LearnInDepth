/* ==========================================================================
   LearnInDepth - Main App Orchestrator (js/app.js)
   ========================================================================== */

const App = (function() {
  const brandBtn = document.getElementById('brandBtn');
  const navDashboardBtn = document.getElementById('navDashboardBtn');
  const navTopicsBtn = document.getElementById('navTopicsBtn');
  const apiStatusBadge = document.getElementById('apiStatusBadge');
  const toastContainer = document.getElementById('toastContainer');

  function init() {
    console.log('🚀 Initializing LearnInDepth AI Learning Platform');

    // Initialize sub-controllers
    AuthController.init();
    DashboardController.init();
    StatusController.init();
    ReaderController.init();
    QuizController.init();
    AssignmentController.init();

    bindEvents();
    checkBackendHealth();
  }

  function bindEvents() {
    brandBtn.addEventListener('click', () => navigate('dashboard'));
    navDashboardBtn.addEventListener('click', () => navigate('dashboard'));
    navTopicsBtn.addEventListener('click', () => navigate('dashboard'));
  }

  async function checkBackendHealth() {
    const isOnline = await ApiClient.checkHealth();
    const dot = apiStatusBadge.querySelector('.status-dot');
    const label = apiStatusBadge.querySelector('.status-label');

    if (isOnline) {
      dot.className = 'status-dot online';
      label.innerText = 'Backend Connected';
    } else {
      dot.className = 'status-dot offline';
      label.innerText = 'Demo Mode (Offline)';
    }
  }

  function navigate(viewName) {
    // Hide all view sections
    document.querySelectorAll('.view-section').forEach(sec => sec.classList.remove('active'));

    // Highlight nav button if dashboard
    if (viewName === 'dashboard') {
      navDashboardBtn.classList.add('active');
      navTopicsBtn.classList.remove('active');
    }

    const targetView = document.getElementById(`view${capitalize(viewName)}`);
    if (targetView) {
      targetView.classList.add('active');
      window.scrollTo(0, 0);
    }
  }

  function showView(viewName) {
    navigate(viewName);
  }

  function openPlan(slug) {
    ReaderController.loadPlan(slug);
  }

  function refreshDashboard() {
    DashboardController.loadTopics();
  }

  function showToast(message, type = 'info') {
    const toast = document.createElement('div');
    toast.className = `toast-msg ${type}`;
    toast.innerText = message;

    toastContainer.appendChild(toast);

    setTimeout(() => {
      toast.style.animation = 'slideInRight 0.3s reverse';
      setTimeout(() => toast.remove(), 300);
    }, 4000);
  }

  function capitalize(str) {
    return str.charAt(0).toUpperCase() + str.slice(1);
  }

  // Auto-bootstrap when DOM ready
  document.addEventListener('DOMContentLoaded', init);

  return {
    navigate,
    showView,
    openPlan,
    refreshDashboard,
    showToast
  };
})();
