/* ==========================================================================
   LearnInDepth - API Client Module (js/api.js)
   ========================================================================== */

const ApiClient = (function() {
  // Production Azure Backend URL
  const AZURE_BACKEND_URL = 'https://learnindepth-cvhda0bzgsgcfsdz.centralindia-01.azurewebsites.net';

  function getBaseUrl() {
    const saved = localStorage.getItem('learnindepth_api_url');
    if (saved) return saved;

    const origin = window.location.origin;
    if (!origin || origin.includes('file://') || origin.includes('127.0.0.1') || origin.includes('localhost')) {
      return AZURE_BACKEND_URL;
    }
    return origin;
  }

  let baseUrl = getBaseUrl();

  // Storage keys
  const TOKEN_KEY = 'learnindepth_auth_token';
  const EMAIL_KEY = 'learnindepth_user_email';

  // Getters & Setters
  function getToken() { return localStorage.getItem(TOKEN_KEY) || ''; }
  function getEmail() { return localStorage.getItem(EMAIL_KEY) || 'guest@learnindepth.local'; }
  
  function setSession(token, email) {
    if (token) localStorage.setItem(TOKEN_KEY, token);
    if (email) localStorage.setItem(EMAIL_KEY, email);
  }

  function clearSession() {
    localStorage.removeItem(TOKEN_KEY);
    localStorage.removeItem(EMAIL_KEY);
  }

  function getHeaders(contentType = 'application/json') {
    const headers = {
      'x-uid': getEmail()
    };
    if (contentType) headers['Content-Type'] = contentType;
    const token = getToken();
    if (token) headers['Authorization'] = `Bearer ${token}`;
    return headers;
  }

  // Generic fetch wrapper with error handling
  async function request(endpoint, options = {}) {
    const url = `${baseUrl}${endpoint}`;
    options.headers = { ...getHeaders(), ...(options.headers || {}) };

    try {
      const response = await fetch(url, options);

      // Handle 401 Unauthorized
      if (response.status === 401) {
        window.dispatchEvent(new CustomEvent('unauthorized_event'));
      }

      // Handle rate limits 429
      if (response.status === 429) {
        throw new Error('Rate limit exceeded. Please wait a few seconds before trying again.');
      }

      const data = await response.json().catch(() => ({}));

      if (!response.ok) {
        throw new Error(data.message || `Server error (${response.status})`);
      }

      return data;
    } catch (err) {
      console.warn(`[ApiClient] Request to ${endpoint} failed:`, err.message);
      // Check if backend is completely down - trigger mock mode if needed
      if (err.name === 'TypeError' && err.message.includes('Fetch')) {
        return getMockData(endpoint, options);
      }
      throw err;
    }
  }

  // =========================================================================
  // API Endpoints
  // =========================================================================

  async function sendOtp(email) {
    return request('/api/SignIn/send-otp', {
      method: 'POST',
      headers: { 'x-uid': email }
    });
  }

  async function verifyOtp(email, otp) {
    return request('/api/SignIn/verify-otp', {
      method: 'POST',
      headers: { 'x-uid': email },
      body: JSON.stringify({ otp: otp })
    });
  }

  async function submitTopic(topic) {
    return request('/api/learn/topics', {
      method: 'POST',
      body: JSON.stringify({ topic: topic })
    });
  }

  async function listTopics() {
    return request('/api/learn/topics');
  }

  async function getTopicStatus(slug) {
    return request(`/api/learn/topics/${slug}/status`);
  }

  async function getTopicPlan(slug) {
    return request(`/api/learn/topics/${slug}/plan`);
  }

  async function getChapterContent(slug, order) {
    return request(`/api/learn/topics/${slug}/chapters/${order}/content`);
  }

  async function getChapterQuiz(slug, order) {
    return request(`/api/learn/topics/${slug}/chapters/${order}/quiz`);
  }

  async function submitQuiz(slug, order, answersMap) {
    return request(`/api/learn/topics/${slug}/chapters/${order}/quiz/submit`, {
      method: 'POST',
      body: JSON.stringify({ answers: answersMap })
    });
  }

  async function getChapterAssignment(slug, order) {
    return request(`/api/learn/topics/${slug}/chapters/${order}/assignment`);
  }

  async function submitAssignment(slug, order, solutionText) {
    return request(`/api/learn/topics/${slug}/chapters/${order}/assignment/submit`, {
      method: 'POST',
      body: JSON.stringify({ solution: solutionText })
    });
  }

  async function retryChapter(slug, order) {
    return request(`/api/learn/topics/${slug}/chapters/${order}/retry`, {
      method: 'POST'
    });
  }

  async function getProgress(slug) {
    return request(`/api/learn/topics/${slug}/progress`);
  }

  // Check health of backend
  async function checkHealth() {
    try {
      const res = await fetch(`${baseUrl}/api/Health`, { signal: AbortSignal.timeout(3000) });
      return res.ok;
    } catch {
      return false;
    }
  }

  // Mock Fallback Generator (Provides immediate rich experience if backend API is not running)
  function getMockData(endpoint, options) {
    console.info('[ApiClient] Generating Mock Data for offline preview:', endpoint);

    if (endpoint.endsWith('/send-otp')) {
      return Promise.resolve({ success: true, message: 'OTP sent (Demo OTP: 123456)' });
    }

    if (endpoint.endsWith('/verify-otp')) {
      return Promise.resolve({
        authToken: 'demo-jwt-token-12345',
        email: getEmail(),
        issuer: 'LearnInDepth',
        audience: 'LearnInDepth'
      });
    }

    if (endpoint === '/api/learn/topics' && options.method === 'POST') {
      const body = JSON.parse(options.body || '{}');
      const topic = body.topic || 'System Design & Scalable Architectures';
      const slug = topic.toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/(^-|-$)/g, '');
      return Promise.resolve({
        topic: topic,
        slug: slug,
        status: 'Generating',
        message: 'Learning plan generation submitted. Poll the status endpoint to track progress.'
      });
    }

    if (endpoint === '/api/learn/topics') {
      return Promise.resolve([
        {
          slug: 'system-design-scalable-architectures',
          topic: 'System Design & Scalable Architectures',
          status: 'Ready',
          totalChapters: 4,
          createdAtUtc: new Date().toISOString(),
          userProgressPercent: 50
        },
        {
          slug: 'rust-memory-safety-concurrency',
          topic: 'Rust Memory Safety & Concurrency',
          status: 'Ready',
          totalChapters: 3,
          createdAtUtc: new Date().toISOString(),
          userProgressPercent: 100
        }
      ]);
    }

    if (endpoint.includes('/status')) {
      const parts = endpoint.split('/');
      const slug = parts[4] || 'system-design-scalable-architectures';
      return Promise.resolve({
        slug: slug,
        topic: slug.replace(/-/g, ' ').toUpperCase(),
        status: 'Ready',
        totalChapters: 4,
        readyChapters: 4,
        failedChapters: 0,
        percentComplete: 100,
        chapters: [
          { order: 1, title: 'Foundations & Architectural Patterns', contentStatus: 'Ready', quizStatus: 'Ready', assignmentStatus: 'Ready' },
          { order: 2, title: 'Data Partitioning & Replication', contentStatus: 'Ready', quizStatus: 'Ready', assignmentStatus: 'Ready' },
          { order: 3, title: 'Distributed Caching Strategies', contentStatus: 'Ready', quizStatus: 'Ready', assignmentStatus: 'Ready' },
          { order: 4, title: 'Consistency Models & Consensus (Raft)', contentStatus: 'Ready', quizStatus: 'Ready', assignmentStatus: 'Ready' }
        ]
      });
    }

    if (endpoint.includes('/plan')) {
      return Promise.resolve({
        slug: 'system-design-scalable-architectures',
        topic: 'System Design & Scalable Architectures',
        status: 'Ready',
        createdAtUtc: new Date().toISOString(),
        chapters: [
          {
            order: 1,
            title: 'Foundations & Architectural Patterns',
            description: 'Deep dive into monolith vs microservices, load balancing algorithms, and asynchronous messaging pipelines.',
            keyConcepts: ['Microservices', 'Load Balancing', 'Message Queues'],
            interviewFocus: ['System Trade-offs', 'Scalability Bottlenecks'],
            contentStatus: 'Ready',
            quizStatus: 'Ready',
            assignmentStatus: 'Ready'
          },
          {
            order: 2,
            title: 'Data Partitioning & Replication',
            description: 'Master sharding strategies, consistent hashing, single-leader vs multi-leader replication, and failover mechanics.',
            keyConcepts: ['Consistent Hashing', 'Sharding', 'Replication'],
            interviewFocus: ['CAP Theorem Trade-offs', 'Data Loss Mitigations'],
            contentStatus: 'Ready',
            quizStatus: 'Ready',
            assignmentStatus: 'Ready'
          }
        ]
      });
    }

    if (endpoint.includes('/content')) {
      return Promise.resolve({
        order: 1,
        title: 'Foundations & Architectural Patterns',
        htmlContent: `
          <h2>1. Introduction to Modern Scalable Architecture</h2>
          <p>When designing high-throughput software systems, engineers face constant trade-offs between availability, consistency, latency, and operational cost.</p>
          
          <h3>Key Principles of High-Scale Systems</h3>
          <ul>
            <li><strong>Stateless Application Tier:</strong> Decouple application logic from volatile session state to scale horizontally behind round-robin or least-connection load balancers.</li>
            <li><strong>Asynchronous Processing:</strong> Offload heavy computational work, email dispatching, and report generations using durable message brokers (Kafka/RabbitMQ).</li>
            <li><strong>Tiered Caching:</strong> Reduce database IOPS by leveraging multi-layer caching strategies (Edge CDN, Redis/Memcached cluster).</li>
          </ul>

          <pre><code>// Example: Least-Connections Load Balancer Algorithm Pseudo-code
class LoadBalancer {
    private List&lt;ServerNode&gt; nodes;

    public ServerNode GetNextNode() {
        return nodes.OrderBy(n =&gt; n.ActiveConnections).First();
    }
}</code></pre>

          <h3>Interview Deep Dive & Trade-offs</h3>
          <p>Be prepared to discuss <strong>Horizontal Scaling vs Vertical Scaling</strong>, database connection pooling constraints, and single-point-of-failure (SPOF) elimination.</p>
        `
      });
    }

    if (endpoint.includes('/quiz') && options.method === 'POST') {
      return Promise.resolve({
        order: 1,
        totalQuestions: 3,
        correctCount: 3,
        scorePercent: 100,
        passed: true,
        results: [
          { questionNumber: 1, wasAnswered: true, selectedOptionIndex: 0, correctOptionIndex: 0, isCorrect: true, explanation: 'Stateless application servers allow seamless horizontal scaling behind load balancers.' },
          { questionNumber: 2, wasAnswered: true, selectedOptionIndex: 1, correctOptionIndex: 1, isCorrect: true, explanation: 'Consistent hashing minimizes key remapping when nodes are added or removed.' },
          { questionNumber: 3, wasAnswered: true, selectedOptionIndex: 2, correctOptionIndex: 2, isCorrect: true, explanation: 'Asynchronous event queues prevent web threads from blocking during long-running tasks.' }
        ]
      });
    }

    if (endpoint.includes('/quiz')) {
      return Promise.resolve({
        order: 1,
        title: 'Foundations & Architectural Patterns Quiz',
        questions: [
          {
            questionNumber: 1,
            question: 'Why is a stateless application tier essential for horizontal scaling in distributed web systems?',
            options: [
              'Any server instance can handle any user request independently',
              'It eliminates the need for any database or persistent storage',
              'It forces all requests to be routed to a single master server',
              'It encrypts session keys inside user cookies automatically'
            ],
            difficulty: 'Medium',
            interviewStyle: true
          },
          {
            questionNumber: 2,
            question: 'What is the primary benefit of consistent hashing in distributed caching networks?',
            options: [
              'It ensures strict ACID transaction compliance',
              'Only K/n keys need to be remapped on cluster node additions/removals',
              'It reduces network latency to zero milliseconds',
              'It compresses cached data automatically'
            ],
            difficulty: 'Hard',
            interviewStyle: true
          }
        ]
      });
    }

    if (endpoint.includes('/assignment') && options.method === 'POST') {
      return Promise.resolve({
        verdict: 'Accepted & Highly Efficient',
        score: 95,
        whatWentWell: [
          'Excellent implementation of connection pooling and error retries.',
          'Proper utilization of exponential backoff for network resilience.'
        ],
        corrections: [
          'Consider adding a fallback circuit breaker pattern to prevent cascading thread exhaustion during downstream service outages.'
        ],
        interviewTips: 'Highlight your understanding of circuit breakers (e.g. Resilience4j / Polly) in senior system design interviews.',
        submittedAtUtc: new Date().toISOString()
      });
    }

    if (endpoint.includes('/assignment')) {
      return Promise.resolve({
        order: 1,
        title: 'Design a Resilient Distributed Load Balancer',
        problemStatement: 'Construct a load balancer simulation in your language of choice that handles server health checks, routes traffic using weighted round-robin, and handles node failure grace periods.',
        tasks: [
          'Implement server node health probing every 5 seconds.',
          'Support weighted traffic distribution among healthy nodes.',
          'Provide failover rerouting when a node returns 5xx status codes.'
        ],
        hints: [
          'Maintain an atomic connection counter for each server node.',
          'Use a thread-safe ring buffer or circular list for server selection.'
        ],
        expectedOutcome: 'A modular, high-performance load balancer class handling edge failures cleanly without dropping active user requests.'
      });
    }

    return Promise.resolve({});
  }

  function setBaseUrl(url) {
    if (url) {
      localStorage.setItem('learnindepth_api_url', url);
      baseUrl = url;
    } else {
      localStorage.removeItem('learnindepth_api_url');
      baseUrl = getBaseUrl();
    }
  }

  return {
    getBaseUrl: () => baseUrl,
    setBaseUrl,
    getToken,
    getEmail,
    setSession,
    clearSession,
    sendOtp,
    verifyOtp,
    submitTopic,
    listTopics,
    getTopicStatus,
    getTopicPlan,
    getChapterContent,
    getChapterQuiz,
    submitQuiz,
    getChapterAssignment,
    submitAssignment,
    retryChapter,
    getProgress,
    checkHealth
  };
})();
