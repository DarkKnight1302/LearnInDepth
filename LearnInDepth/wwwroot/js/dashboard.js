/* ==========================================================================
   LearnInDepth - Dashboard & Topic Creation Controller (js/dashboard.js)
   ========================================================================== */

const DashboardController = (function() {
  const topicForm = document.getElementById('topicForm');
  const topicInput = document.getElementById('topicInput');
  const submitTopicBtn = document.getElementById('submitTopicBtn');
  const promptPills = document.getElementById('promptPills');
  const topicsGrid = document.getElementById('topicsGrid');
  const librarySearchInput = document.getElementById('librarySearchInput');

  let allTopics = [];
  let currentFilter = 'all';
  let searchQuery = '';

  function init() {
    bindEvents();
    loadTopics();
  }

  function bindEvents() {
    // Topic Form Submit
    topicForm.addEventListener('submit', async (e) => {
      e.preventDefault();
      const topic = topicInput.value.trim();
      if (!topic) return;

      submitTopicBtn.disabled = true;
      submitTopicBtn.querySelector('span').innerText = 'Submitting...';

      try {
        const res = await ApiClient.submitTopic(topic);
        topicInput.value = '';
        App.showToast(res.message || 'Topic submitted successfully!', 'success');

        if (res.status === 'Ready') {
          App.openPlan(res.slug);
        } else {
          // Open real-time generation monitor
          StatusController.monitor(res.slug, topic);
        }
      } catch (err) {
        App.showToast(err.message || 'Failed to submit topic', 'error');
      } finally {
        submitTopicBtn.disabled = false;
        submitTopicBtn.querySelector('span').innerText = 'Generate Course';
      }
    });

    // Popular Topic Pills
    promptPills.addEventListener('click', (e) => {
      const pill = e.target.closest('.pill-btn');
      if (pill) {
        const topic = pill.getAttribute('data-topic');
        topicInput.value = topic;
        topicInput.focus();
      }
    });

    // Filter Chips
    document.querySelectorAll('.filter-chip').forEach(chip => {
      chip.addEventListener('click', () => {
        document.querySelectorAll('.filter-chip').forEach(c => c.classList.remove('active'));
        chip.classList.add('active');
        currentFilter = chip.getAttribute('data-filter');
        renderGrid();
      });
    });

    // Search Box
    librarySearchInput.addEventListener('input', (e) => {
      searchQuery = e.target.value.toLowerCase().trim();
      renderGrid();
    });
  }

  async function loadTopics() {
    renderSkeletons();
    try {
      allTopics = await ApiClient.listTopics();
      renderGrid();
    } catch (err) {
      console.error('Failed to load topics:', err);
      topicsGrid.innerHTML = `
        <div class="empty-state">
          <p>Failed to load learning library. <button onclick="DashboardController.loadTopics()" class="link-btn">Try Again</button></p>
        </div>
      `;
    }
  }

  function renderSkeletons() {
    topicsGrid.innerHTML = Array(3).fill(0).map(() => `
      <div class="topic-card glass-card">
        <div class="topic-card-top">
          <div style="width: 60%; height: 20px; background: rgba(255,255,255,0.06); border-radius: 4px;"></div>
          <div style="width: 20%; height: 16px; background: rgba(255,255,255,0.06); border-radius: 10px;"></div>
        </div>
        <div style="width: 40%; height: 14px; background: rgba(255,255,255,0.04); border-radius: 4px; margin-top: 12px;"></div>
      </div>
    `).join('');
  }

  function renderGrid() {
    let filtered = allTopics.filter(t => {
      const matchesFilter = currentFilter === 'all' || t.status === currentFilter;
      const matchesSearch = !searchQuery || t.topic.toLowerCase().includes(searchQuery);
      return matchesFilter && matchesSearch;
    });

    if (filtered.length === 0) {
      topicsGrid.innerHTML = `
        <div class="empty-state-panel glass-card" style="grid-column: 1 / -1; padding: 40px; text-align: center;">
          <h3 style="margin-bottom: 8px;">No Topics Found</h3>
          <p style="color: var(--text-muted);">Start learning by typing a technical topic in the prompt box above!</p>
        </div>
      `;
      return;
    }

    topicsGrid.innerHTML = filtered.map(t => {
      const pct = t.userProgressPercent !== null && t.userProgressPercent !== undefined ? t.userProgressPercent : 0;
      const statusClass = t.status === 'Ready' ? 'ready' : (t.status === 'Generating' ? 'generating' : 'failed');

      return `
        <div class="topic-card glass-card" onclick="DashboardController.handleCardClick('${t.slug}', '${t.status}', '${escapeHtml(t.topic)}')">
          <div class="topic-card-top">
            <h3 class="topic-title">${escapeHtml(t.topic)}</h3>
            <span class="status-badge ${statusClass}">${t.status}</span>
          </div>

          <div class="topic-card-bottom">
            <div class="topic-meta-row">
              <span>${t.totalChapters} Chapters</span>
              <span>${pct}% Mastered</span>
            </div>
            <div class="card-progress-bar">
              <div class="card-progress-fill" style="width: ${pct}%"></div>
            </div>
          </div>
        </div>
      `;
    }).join('');
  }

  function handleCardClick(slug, status, topicTitle) {
    if (status === 'Generating') {
      StatusController.monitor(slug, topicTitle);
    } else {
      App.openPlan(slug);
    }
  }

  function escapeHtml(str) {
    return str.replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;").replace(/"/g, "&quot;");
  }

  return {
    init,
    loadTopics,
    handleCardClick
  };
})();
