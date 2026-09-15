(() => {
  const permissions = {
    accessManage: "access.manage",
    stockRead: "stock.read",
    stockMove: "stock.move",
    productsManage: "products.manage",
    catalogImport: "catalog.import",
    employeesManage: "employees.manage",
    remindersManage: "reminders.manage"
  };

  const elements = {};
  const state = {
    auth: null,
    view: "stock",
    products: [],
    employees: [],
    movements: [],
    reminders: [],
    reminderDelivery: null,
    powerAutomateSettings: null,
    catalogImportPreview: null,
    screenVisibility: { hiddenScreensByRole: { admin: [], standard: [] } },
    access: { catalog: null, users: [], approvals: [], approvalFlowSettings: null },
    drafts: { product: null, employee: null, reminder: null },
    pendingPassword: null,
    sessionSyncTimer: null,
    sidebarCollapsed: readStoredBoolean("generic-inventory.sidebarCollapsed"),
    language: readStoredString("generic-inventory.language", "pt") === "en" ? "en" : "pt",
    theme: document.documentElement.dataset.theme === "dark" ? "dark" : "light"
  };

  const views = [
    { id: "stock", label: { pt: "Catálogo", en: "Catalog" }, icon: "package-search", permission: permissions.stockRead, title: { pt: "Catálogo", en: "Catalog" }, subtitle: { pt: "Itens, imagens, estoque e informações personalizadas.", en: "Items, images, stock levels, and custom information." } },
    { id: "out", label: { pt: "Saída", en: "Stock Out" }, icon: "log-out", permission: permissions.stockMove, title: { pt: "Saída", en: "Stock Out" }, subtitle: { pt: "Registre baixa de estoque.", en: "Register inventory withdrawals." } },
    { id: "in", label: { pt: "Entrada", en: "Stock In" }, icon: "log-in", permission: permissions.stockMove, title: { pt: "Entrada", en: "Stock In" }, subtitle: { pt: "Registre reposição de estoque.", en: "Register inventory replenishment." } },
    { id: "movements", label: { pt: "Movimentações", en: "Movements" }, icon: "list-filter", permission: permissions.stockRead, title: { pt: "Movimentações", en: "Movements" }, subtitle: { pt: "Histórico de entradas e saídas.", en: "History of stock entries and withdrawals." } },
    { id: "products", label: { pt: "Itens", en: "Items" }, icon: "boxes", permission: permissions.productsManage, title: { pt: "Itens", en: "Items" }, subtitle: { pt: "Cadastro flexível do catálogo.", en: "Flexible catalog item registration." } },
    { id: "employees", label: { pt: "Funcionários", en: "Employees" }, icon: "users", permission: permissions.employeesManage, title: { pt: "Funcionários", en: "Employees" }, subtitle: { pt: "Pessoas disponíveis para movimentações.", en: "People available for inventory movements." } },
    { id: "reminders", label: { pt: "Lembretes", en: "Reminders" }, icon: "bell-ring", permission: permissions.remindersManage, title: { pt: "Lembretes", en: "Reminders" }, subtitle: { pt: "Regras editáveis de alerta de estoque.", en: "Editable stock alert rules." } },
    { id: "access", label: { pt: "Acessos", en: "Access" }, icon: "shield-check", permission: permissions.accessManage, title: { pt: "Acessos", en: "Access" }, subtitle: { pt: "Contas, convites e aprovações.", en: "Accounts, invitations, and approvals." } },
    { id: "docs", label: { pt: "Documentação", en: "Documentation" }, icon: "book-open", permission: null, title: { pt: "Documentação", en: "Documentation" }, subtitle: { pt: "Guia rápido de uso, acesso global e responsabilidades.", en: "Quick guide for usage, global access, and responsibilities." } },
    { id: "releases", label: { pt: "Releases", en: "Releases" }, icon: "history", permission: null, title: { pt: "Releases", en: "Releases" }, subtitle: { pt: "Histórico das versões publicadas do serviço.", en: "History of published service versions." } }
  ];

  const screenVisibilityRoles = [
    { role: "admin", label: "Admin" },
    { role: "standard", label: "User" }
  ];

  document.addEventListener("DOMContentLoaded", init);
  registerServiceWorker();

  async function init() {
    bindElements();
    document.documentElement.lang = state.language === "en" ? "en" : "pt-BR";
    setTheme(state.theme, false);
    bindEvents();
    readPasswordInvite();
    await refreshSession();
  }

  function bindElements() {
    [
      "authGate", "appShell", "authHint", "loginForm", "registerForm", "passwordForm",
      "authMessage", "navMenu", "sidebarToggle", "content", "viewTitle", "viewSubtitle", "userMenu", "toast", "siteFooter"
    ].forEach((id) => {
      elements[id] = document.getElementById(id);
    });
  }

  function bindEvents() {
    document.querySelectorAll("[data-auth-tab]").forEach((button) => {
      button.addEventListener("click", () => setAuthTab(button.dataset.authTab));
    });

    elements.loginForm.addEventListener("submit", (event) => {
      event.preventDefault();
      login(new FormData(event.currentTarget)).catch(showError);
    });

    elements.registerForm.addEventListener("submit", (event) => {
      event.preventDefault();
      register(new FormData(event.currentTarget)).catch(showError);
    });

    elements.passwordForm.addEventListener("submit", (event) => {
      event.preventDefault();
      resetPassword(new FormData(event.currentTarget)).catch(showError);
    });

    elements.authGate.addEventListener("click", (event) => {
      if (event.target.closest("[data-theme-toggle]")) {
        toggleTheme();
        return;
      }

      if (event.target.closest("[data-language-toggle]")) {
        toggleLanguage();
        return;
      }

      if (event.target.closest("[data-forgot-password]")) {
        forgotPassword().catch(showError);
      }
    });

    elements.navMenu.addEventListener("click", (event) => {
      const button = event.target.closest("[data-view]");
      if (button) {
        setView(button.dataset.view);
      }
    });

    elements.sidebarToggle.addEventListener("click", toggleSidebar);

    elements.userMenu.addEventListener("click", (event) => {
      if (event.target.closest("[data-theme-toggle]")) {
        toggleTheme();
      } else if (event.target.closest("[data-language-toggle]")) {
        toggleLanguage();
      } else if (event.target.closest("[data-logout]")) {
        logout().catch(showError);
      }
    });

    elements.content.addEventListener("click", handleContentClick);
    elements.content.addEventListener("submit", handleContentSubmit);
    elements.content.addEventListener("input", handleContentInput);
    elements.content.addEventListener("change", handleContentChange);
    elements.content.addEventListener("dragover", handleContentDragOver);
    elements.content.addEventListener("dragleave", handleContentDragLeave);
    elements.content.addEventListener("drop", handleContentDrop);
    window.addEventListener("resize", () => {
      if (isMobileMenu()) {
        state.sidebarCollapsed = true;
        applySidebarState();
      }
    });
  }

  function readPasswordInvite() {
    const params = new URLSearchParams(window.location.search);
    if (params.get("view") === "password" && params.get("uid") && params.get("token")) {
      state.pendingPassword = { id: params.get("uid"), token: params.get("token") };
      setAuthTab("password");
    }
  }

  async function refreshSession() {
    state.auth = await authJson("/me", { method: "GET" });
    if (state.auth.isAuthenticated) {
      await loadScreenVisibility();
      showApp();
      await loadCurrentView();
    } else {
      showAuth();
    }
  }

  async function login(formData) {
    setAuthMessage("Entrando...");
    state.auth = await authJson("/login", {
      method: "POST",
      body: JSON.stringify({
        email: String(formData.get("email") || "").trim(),
        password: String(formData.get("password") || "")
      })
    });
    showApp();
    await loadCurrentView();
  }

  async function register(formData) {
    setAuthMessage("Enviando cadastro...");
    await authJson("/register", {
      method: "POST",
      body: JSON.stringify({
        name: String(formData.get("name") || "").trim(),
        email: String(formData.get("email") || "").trim(),
        password: String(formData.get("password") || "")
      })
    });
    setAuthTab("login");
    setAuthMessage("Cadastro enviado para aprovação.", "success");
  }

  async function forgotPassword() {
    const email = String(new FormData(elements.loginForm).get("email") || "").trim();
    if (!email) {
      throw new Error("Informe o e-mail antes de pedir o link.");
    }

    await authJson("/password/forgot", {
      method: "POST",
      body: JSON.stringify({ email })
    });
    setAuthMessage("Se o e-mail estiver cadastrado, o link será enviado.", "success");
  }

  async function resetPassword(formData) {
    if (!state.pendingPassword) {
      throw new Error("Abra esta tela pelo link recebido.");
    }

    const password = String(formData.get("password") || "");
    const confirmation = String(formData.get("confirmation") || "");
    if (password !== confirmation) {
      throw new Error("As senhas não conferem.");
    }

    await authJson(`/password/reset/${encodeURIComponent(state.pendingPassword.id)}`, {
      method: "POST",
      body: JSON.stringify({ token: state.pendingPassword.token, password })
    });
    state.pendingPassword = null;
    window.history.replaceState({}, document.title, "/");
    setAuthTab("login");
    setAuthMessage("Senha definida. Entre com seu e-mail.", "success");
  }

  async function logout() {
    await authJson("/logout", { method: "POST" });
    state.auth = null;
    showAuth();
  }

  async function authJson(path, options = {}) {
    return json(`/api/auth${path}`, options);
  }

  async function accessJson(path, options = {}) {
    return json(`/api/access${path}`, options);
  }

  async function json(path, options = {}) {
    const headers = { ...(options.headers || {}) };
    if (options.body && !(options.body instanceof FormData) && !headers["Content-Type"]) {
      headers["Content-Type"] = "application/json";
    }

    const response = await fetch(path, {
      credentials: "same-origin",
      ...options,
      headers
    });

    if (!response.ok) {
      let message = `Erro HTTP ${response.status}.`;
      try {
        const body = await response.json();
        message = body.message || message;
      } catch {
        message = await response.text() || message;
      }

      if (response.status === 401) {
        state.auth = null;
        showAuth();
        throw new Error(message === `Erro HTTP ${response.status}.` ? "Sessao expirada." : message);
      }

      throw new Error(message);
    }

    if (response.status === 204) {
      return null;
    }

    return response.json();
  }

  function showAuth() {
    stopSessionSync();
    elements.appShell.classList.add("hidden");
    elements.authGate.classList.remove("hidden");
    const canRegister = state.auth?.selfRegistrationEnabled !== false;
    document.querySelector('[data-auth-tab="register"]').disabled = !canRegister;
    renderThemeToggleButtons();
    refreshIcons();
  }

  function showApp() {
    elements.authGate.classList.add("hidden");
    elements.appShell.classList.remove("hidden");
    if (isMobileMenu()) {
      state.sidebarCollapsed = true;
    }
    applySidebarState();
    renderNav();
    renderUserMenu();
    renderFooter();
    startSessionSync();
  }

  function setAuthTab(tab) {
    document.querySelectorAll("[data-auth-tab]").forEach((button) => {
      button.classList.toggle("active", button.dataset.authTab === tab);
    });
    document.querySelectorAll(".auth-form").forEach((form) => {
      form.classList.toggle("active", form.id === `${tab}Form`);
    });
    const messages = {
      login: "Entre com uma conta liberada pelo administrador.",
      register: "O administrador define o perfil de acesso.",
      password: "Defina uma senha de pelo menos 8 caracteres."
    };
    elements.authHint.textContent = messages[tab] || messages.login;
    setAuthMessage("");
  }

  function setAuthMessage(message, type = "") {
    elements.authMessage.textContent = message;
    elements.authMessage.className = `message ${type}`.trim();
  }

  function renderNav() {
    const available = availableViews();
    ensureCurrentViewAvailable(available);

    elements.navMenu.innerHTML = available.map((view, index) => `
      <button class="nav-item ${state.view === view.id ? "active" : ""}" type="button" data-view="${view.id}" title="${escapeAttribute(viewText(view, "label"))}" style="--entry-delay: ${index * 28}ms">
        <i data-lucide="${escapeAttribute(view.icon)}"></i>
        <span>${escapeHtml(viewText(view, "label"))}</span>
      </button>
    `).join("");
    refreshIcons();
  }

  function availableViews() {
    return views.filter((view) => can(view.permission) && !isScreenHiddenForCurrentRole(view.id));
  }

  function ensureCurrentViewAvailable(available = availableViews()) {
    if (!available.some((view) => view.id === state.view)) {
      state.view = available[0]?.id || "stock";
      return true;
    }

    return false;
  }

  function toggleSidebar() {
    if (isMobileMenu()) {
      state.sidebarCollapsed = !state.sidebarCollapsed;
      applySidebarState();
      return;
    }

    state.sidebarCollapsed = !state.sidebarCollapsed;
    try {
      localStorage.setItem("generic-inventory.sidebarCollapsed", state.sidebarCollapsed ? "true" : "false");
    } catch {
      // Sidebar persistence is optional; the toggle still works for the session.
    }
    applySidebarState();
  }

  function applySidebarState() {
    elements.appShell.classList.toggle("sidebar-collapsed", state.sidebarCollapsed);
    const mobile = isMobileMenu();
    elements.sidebarToggle.title = state.sidebarCollapsed ? "Expandir menu" : "Recolher menu";
    elements.sidebarToggle.setAttribute("aria-label", elements.sidebarToggle.title);
    elements.sidebarToggle.setAttribute("aria-expanded", String(!state.sidebarCollapsed));
    elements.sidebarToggle.innerHTML = mobile
      ? `<i data-lucide="menu"></i><span>Menu</span>`
      : `<i data-lucide="${state.sidebarCollapsed ? "panel-left-open" : "panel-left-close"}"></i>`;
    refreshIcons();
  }

  function isMobileMenu() {
    return window.matchMedia("(max-width: 900px)").matches;
  }

  function renderUserMenu() {
    const user = state.auth?.user;
    elements.userMenu.innerHTML = user ? `
      <div class="user-chip">
        <div>
          <strong>${escapeHtml(user.name)}</strong>
          <span>${escapeHtml(user.roleLabel || user.role)}</span>
        </div>
      </div>
      <button class="button ghost theme-toggle" type="button" data-theme-toggle title="${themeToggleLabel()}" aria-label="${themeToggleLabel()}">Tema</button>
      <button class="button ghost language-toggle" type="button" data-language-toggle title="${languageToggleLabel()}" aria-label="${languageToggleLabel()}"><i data-lucide="languages"></i><span>${state.language.toUpperCase()}</span></button>
      <button class="button ghost" type="button" data-logout><i data-lucide="log-out"></i><span>${state.language === "en" ? "Sign out" : "Sair"}</span></button>
    ` : "";
    refreshIcons();
  }

  function toggleLanguage() {
    setLanguage(state.language === "pt" ? "en" : "pt");
  }

  function setLanguage(language, persist = true) {
    state.language = language === "en" ? "en" : "pt";
    document.documentElement.lang = state.language === "en" ? "en" : "pt-BR";

    if (persist) {
      try {
        localStorage.setItem("generic-inventory.language", state.language);
      } catch {
        // Language persistence is optional; the toggle still works for the session.
      }
    }

    renderNav();
    renderUserMenu();
    renderThemeToggleButtons();
    renderLanguageToggleButtons();
    renderFooter();
    loadCurrentView().catch(showError);
  }

  function languageToggleLabel() {
    return state.language === "pt" ? "Switch to English" : "Mudar para português";
  }

  function renderLanguageToggleButtons() {
    const label = languageToggleLabel();
    document.querySelectorAll("[data-language-toggle]").forEach((button) => {
      button.title = label;
      button.setAttribute("aria-label", label);
      const text = state.language === "en" ? "EN" : "PT";
      if (button.classList.contains("icon-button")) {
        button.innerHTML = `<i data-lucide="languages"></i><span class="sr-only">${escapeHtml(text)}</span>`;
      } else {
        button.innerHTML = `<i data-lucide="languages"></i><span>${escapeHtml(text)}</span>`;
      }
    });
    refreshIcons();
  }

  function toggleTheme() {
    setTheme(state.theme === "dark" ? "light" : "dark");
  }

  function setTheme(theme, persist = true) {
    state.theme = theme === "dark" ? "dark" : "light";
    document.documentElement.dataset.theme = state.theme;
    document.querySelector('meta[name="theme-color"]')
      ?.setAttribute("content", state.theme === "dark" ? "#101513" : "#f6f7f4");

    if (persist) {
      try {
        localStorage.setItem("generic-inventory.theme", state.theme);
      } catch {
        // Theme persistence is optional; the toggle still works for the session.
      }
    }

    renderThemeToggleButtons();
    renderLanguageToggleButtons();
  }

  function renderThemeToggleButtons() {
    const label = themeToggleLabel();
    document.querySelectorAll("[data-theme-toggle]").forEach((button) => {
      button.title = label;
      button.setAttribute("aria-label", label);
      const text = state.theme === "dark"
        ? (state.language === "en" ? "Light theme" : "Tema claro")
        : (state.language === "en" ? "Dark theme" : "Tema escuro");
      button.innerHTML = `<i data-lucide="${state.theme === "dark" ? "sun" : "moon"}"></i><span class="button-label">${text}</span>`;
    });
    refreshIcons();
  }

  function themeToggleLabel() {
    if (state.language === "en") {
      return state.theme === "dark" ? "Use light theme" : "Use dark theme";
    }

    return state.theme === "dark" ? "Usar tema claro" : "Usar tema escuro";
  }

  async function setView(view) {
    state.view = view;
    state.drafts = { product: null, employee: null, reminder: null };
    renderNav();
    await loadCurrentView();
  }

  async function loadCurrentView() {
    const view = views.find((item) => item.id === state.view) || views[0];
    elements.viewTitle.textContent = viewText(view, "title");
    elements.viewSubtitle.textContent = viewText(view, "subtitle");

    if (state.view === "stock") return renderStock();
    if (state.view === "out") return renderMovementForm("out");
    if (state.view === "in") return renderMovementForm("in");
    if (state.view === "movements") return renderMovements();
    if (state.view === "products") return renderProductsAdmin();
    if (state.view === "employees") return renderEmployeesAdmin();
    if (state.view === "reminders") return renderReminders();
    if (state.view === "access") return renderAccess();
    if (state.view === "docs") return renderDocumentation();
    if (state.view === "releases") return renderReleases();
  }

  async function loadScreenVisibility() {
    try {
      state.screenVisibility = await json("/api/access/screen-visibility");
    } catch {
      state.screenVisibility = { hiddenScreensByRole: { admin: [], standard: [] } };
    }
  }

  function startSessionSync() {
    stopSessionSync();
    state.sessionSyncTimer = window.setInterval(() => {
      syncSessionState().catch(() => {
        // A proxima sincronizacao tenta de novo; erros autenticados seguem pelo fluxo normal do json().
      });
    }, 10000);
  }

  function stopSessionSync() {
    if (state.sessionSyncTimer) {
      window.clearInterval(state.sessionSyncTimer);
      state.sessionSyncTimer = null;
    }
  }

  async function syncSessionState() {
    if (!state.auth?.isAuthenticated) {
      return;
    }

    const before = accessStateSignature();
    state.auth = await authJson("/me", { method: "GET" });
    if (!state.auth.isAuthenticated) {
      showAuth();
      return;
    }

    await loadScreenVisibility();
    const changedView = ensureCurrentViewAvailable();
    const changedAccess = before !== accessStateSignature();

    if (isMobileMenu()) {
      state.sidebarCollapsed = true;
      applySidebarState();
    }

    renderNav();
    renderUserMenu();
    renderFooter();

    if (changedView || changedAccess) {
      await loadCurrentView();
    }
  }

  function accessStateSignature() {
    return JSON.stringify({
      role: state.auth?.user?.role || "",
      permissions: state.auth?.permissions || [],
      hiddenScreensByRole: state.screenVisibility?.hiddenScreensByRole || {}
    });
  }

  async function loadProducts(search = "", criticalOnly = false) {
    const params = new URLSearchParams();
    if (search) params.set("search", search);
    if (criticalOnly) params.set("criticalOnly", "true");
    const query = params.toString();
    state.products = await json(`/api/products${query ? `?${query}` : ""}`);
  }

  async function loadEmployees() {
    state.employees = await json("/api/employees");
  }

  async function loadMovements() {
    state.movements = await json("/api/movements");
  }

  async function renderStock(search = "", criticalOnly = false) {
    await Promise.all([loadProducts(search, criticalOnly), loadMovements()]);
    const total = state.products.length;
    const critical = state.products.filter((product) => product.isCritical).length;
    const stock = state.products.reduce((sum, product) => sum + Number(product.currentStock || 0), 0);
    const recent = state.movements.slice(0, 5);

    elements.content.innerHTML = `
      <section class="metric-grid">
        ${metric("Itens", total)}
        ${metric("Críticos", critical, critical ? "bad" : "good")}
        ${metric("Estoque total", formatNumber(stock))}
        ${metric("Movimentos", state.movements.length)}
      </section>
      <section class="panel toolbar">
        <input id="stockSearch" value="${escapeAttribute(search)}" placeholder="Buscar código, nome ou campo personalizado">
        <button class="button secondary" type="button" data-critical-only>Críticos</button>
      </section>
      <section class="product-grid">
        ${state.products.map((product, index) => renderProductCard(product, index)).join("") || empty("Nenhum produto encontrado.")}
      </section>
      <section class="panel">
        <h2>Movimentos recentes</h2>
        ${renderMovementTable(recent)}
      </section>
    `;
  }

  function renderProductCard(product, index = 0) {
    return `
      <article class="product-card" style="--entry-delay: ${Math.min(index, 12) * 34}ms">
        ${renderProductImage(product)}
        <div class="product-body">
          <div>
            <span class="tag">${escapeHtml(product.code)}</span>
            <h3>${escapeHtml(product.description)}</h3>
          </div>
          <div class="tag-row">
            <span class="tag ${product.isCritical ? "bad" : "good"}">Atual ${formatNumber(product.currentStock)}</span>
            <span class="tag">Min ${formatNumber(product.minimumStock)}</span>
            <span class="tag">R$ ${formatMoney(product.saleValue)}</span>
          </div>
          ${renderCustomFieldChips(product.customFields)}
          ${can(permissions.stockMove) ? `
            <div class="form-actions">
              <button class="button ghost" type="button" data-stock-out="${escapeAttribute(product.code)}"><i data-lucide="minus"></i><span>Saída</span></button>
              <button class="button secondary" type="button" data-stock-in="${escapeAttribute(product.code)}"><i data-lucide="plus"></i><span>Entrada</span></button>
            </div>
          ` : ""}
        </div>
      </article>
    `;
  }

  function renderProductImage(product) {
    const imageUrl = product.imagePath || product.legacyImageUrl;
    if (imageUrl) {
      return `<img class="product-image" src="${escapeAttribute(imageUrl)}" alt="${escapeAttribute(product.description)}" loading="lazy">`;
    }

    return `<div class="product-image placeholder" aria-hidden="true">${escapeHtml(productInitials(product))}</div>`;
  }

  function renderCustomFieldChips(fields = []) {
    if (!fields.length) return "";
    return `
      <dl class="field-chip-list">
        ${fields.slice(0, 6).map((field) => `
          <div>
            <dt>${escapeHtml(field.name || "Campo")}</dt>
            <dd>${escapeHtml(field.value || "-")}</dd>
          </div>
        `).join("")}
      </dl>
    `;
  }

  async function renderMovementForm(kind) {
    await Promise.all([loadProducts(), loadEmployees()]);
    const typeLabel = kind === "out" ? "Saída" : "Entrada";
    const productOptions = state.products.map((product) => `
      <option value="${escapeAttribute(product.code)}">${escapeHtml(product.code)} - ${escapeHtml(product.description)}</option>
    `).join("");
    const employeeOptions = state.employees.map((employee) => `
      <option value="${employee.id}">${escapeHtml(employee.name)} - ${escapeHtml(employee.registration)}</option>
    `).join("");

    elements.content.innerHTML = `
      <section class="two-columns">
        <form class="panel form-grid" data-movement-form="${kind}">
          <label>
            <span>Produto</span>
            <select name="productCode" required>${productOptions}</select>
          </label>
          <label>
            <span>Quantidade</span>
            <input name="quantity" type="number" min="1" max="1000" step="0.01" required>
          </label>
          <label>
            <span>Funcionário</span>
            <select name="employeeId">
              <option value="">Sem funcionário</option>
              ${employeeOptions}
            </select>
          </label>
          <button class="button primary" type="submit"><i data-lucide="check"></i><span>Registrar ${typeLabel}</span></button>
        </form>
        <section class="product-grid">
          ${state.products.slice(0, 6).map((product, index) => renderProductCard(product, index)).join("")}
        </section>
      </section>
    `;
    refreshIcons();
  }

  async function renderMovements() {
    await loadMovements();
    elements.content.innerHTML = `
      <section class="panel">
        <h2>Histórico</h2>
        ${renderMovementTable(state.movements)}
      </section>
    `;
  }

  async function renderProductsAdmin() {
    await loadProducts();
    const draft = state.drafts.product || {};
    elements.content.innerHTML = `
      <section class="two-columns">
        <form class="panel form-grid two" data-product-form>
          <input type="hidden" name="originalCode" value="${escapeAttribute(draft.code || "")}">
          ${input("Código", "code", draft.code || "", true)}
          ${input("Nome/descrição", "description", draft.description || "", true)}
          ${input("Estoque atual", "currentStock", draft.currentStock ?? 0, true, "number")}
          ${input("Estoque mínimo", "minimumStock", draft.minimumStock ?? 0, true, "number")}
          ${input("Valor venda", "saleValue", draft.saleValue ?? 0, false, "number")}
          ${productImageField(draft)}
          <section class="custom-fields-editor">
            <div class="panel-heading">
              <h2>Campos personalizados</h2>
              <button class="button ghost" type="button" data-add-product-field><i data-lucide="plus"></i><span>Adicionar campo</span></button>
            </div>
            <div class="custom-field-list">
              ${renderCustomFieldInputs(draft.customFields)}
            </div>
          </section>
          <div class="form-actions">
            <button class="button primary" type="submit"><i data-lucide="save"></i><span>${draft.code ? "Salvar item" : "Criar item"}</span></button>
            <button class="button ghost" type="button" data-clear-product><i data-lucide="eraser"></i><span>Limpar</span></button>
          </div>
        </form>
        <section class="table-card">
          ${renderProductsTable()}
        </section>
      </section>
      ${can(permissions.catalogImport) ? renderCatalogImportPanel() : ""}
    `;
    refreshIcons();
  }

  function renderCatalogImportPanel() {
    const preview = state.catalogImportPreview;
    return `
      <section class="panel catalog-import-panel">
        <div class="panel-heading">
          <div>
            <h2>Importação Developer</h2>
            <p>Importe catálogo por XLSX, CSV ou PDF pesquisável e escolha quais campos serão aplicados.</p>
          </div>
          <span class="tag good">Developer</span>
        </div>
        <form class="form-grid" data-catalog-preview-form>
          <label>
            <span>Arquivo do catálogo</span>
            <input name="file" type="file" accept=".xlsx,.csv,.pdf,application/pdf,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet,text/csv" required>
          </label>
          <button class="button secondary" type="submit"><i data-lucide="scan-search"></i><span>Carregar prévia</span></button>
        </form>
        ${preview ? renderCatalogImportMapping(preview) : ""}
      </section>
    `;
  }

  function renderCatalogImportMapping(preview) {
    const columns = preview.columns || [];
    const sampleRows = preview.rows || [];
    return `
      <form class="form-grid two catalog-mapping" data-catalog-import-form>
        <input type="hidden" name="previewFileReady" value="true">
        ${columnSelect("Código", "codeColumn", columns, guessColumn(columns, ["CODIGO", "CODE", "SKU", "ID"]), true)}
        ${columnSelect("Nome/descrição", "descriptionColumn", columns, guessColumn(columns, ["DESCRICAO", "DESCRIPTION", "NOME", "NAME", "ITEM"]), true)}
        ${columnSelect("Estoque atual", "currentStockColumn", columns, guessColumn(columns, ["ESTOQUEATUAL", "CURRENTSTOCK", "QTD", "QUANTIDADE", "STOCK"]))}
        ${columnSelect("Estoque mínimo", "minimumStockColumn", columns, guessColumn(columns, ["ESTOQUEMINIMO", "MINIMUMSTOCK", "MINIMO", "MIN"]))}
        ${columnSelect("Valor venda", "saleValueColumn", columns, guessColumn(columns, ["VALORVENDA", "SALEVALUE", "PRECO", "PRICE", "VALOR"]))}
        ${columnSelect("Imagem/URL", "imagePathColumn", columns, guessColumn(columns, ["IMAGEM", "IMAGE", "FOTO", "PHOTO", "URL"]))}
        <label class="custom-column-picker">
          <span>Campos personalizados a criar/atualizar</span>
          <select name="customColumns" multiple size="${Math.min(Math.max(columns.length, 4), 9)}">
            ${columns.map((column) => `<option value="${escapeAttribute(column)}">${escapeHtml(column)}</option>`).join("")}
          </select>
        </label>
        <label>
          <span>Reenviar arquivo para importar</span>
          <input name="file" type="file" accept=".xlsx,.csv,.pdf,application/pdf,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet,text/csv" required>
        </label>
        <div class="form-actions">
          <button class="button primary" type="submit"><i data-lucide="upload-cloud"></i><span>Importar catálogo</span></button>
        </div>
      </form>
      <div class="import-preview">
        <strong>${preview.totalRows} linha(s) detectada(s)</strong>
        <div class="table-card">
          <table>
            <thead><tr>${columns.map((column) => `<th>${escapeHtml(column)}</th>`).join("")}</tr></thead>
            <tbody>
              ${sampleRows.map((row) => `<tr>${columns.map((column) => `<td>${escapeHtml(row[column] || "")}</td>`).join("")}</tr>`).join("")}
            </tbody>
          </table>
        </div>
      </div>
    `;
  }

  function columnSelect(label, name, columns, selected = "", required = false) {
    return `
      <label>
        <span>${escapeHtml(label)}</span>
        <select name="${escapeAttribute(name)}" ${required ? "required" : ""}>
          <option value="">Não importar</option>
          ${columns.map((column) => `<option value="${escapeAttribute(column)}" ${column === selected ? "selected" : ""}>${escapeHtml(column)}</option>`).join("")}
        </select>
      </label>
    `;
  }

  function guessColumn(columns, candidates) {
    return columns.find((column) => candidates.includes(normalizeHeader(column))) || "";
  }

  function renderProductsTable() {
    return `
      <table>
        <thead><tr><th>Código</th><th>Item</th><th>Atual</th><th>Mínimo</th><th></th></tr></thead>
        <tbody>
          ${state.products.map((product) => `
            <tr>
              <td>${escapeHtml(product.code)}</td>
              <td>${escapeHtml(product.description)}</td>
              <td>${formatNumber(product.currentStock)}</td>
              <td>${formatNumber(product.minimumStock)}</td>
              <td class="actions">
                <button class="button secondary" type="button" data-edit-product="${escapeAttribute(product.code)}"><i data-lucide="pencil"></i><span>Editar</span></button>
                <button class="button danger" type="button" data-delete-product="${escapeAttribute(product.code)}"><i data-lucide="trash-2"></i><span>Remover</span></button>
              </td>
            </tr>
          `).join("")}
        </tbody>
      </table>
    `;
  }

  function renderCustomFieldInputs(fields = []) {
    const rows = fields.length ? fields : [{ name: "", value: "" }];
    return rows.map((field) => `
      <div class="custom-field-row">
        <input name="customFieldName" value="${escapeAttribute(field.name || "")}" placeholder="Campo">
        <input name="customFieldValue" value="${escapeAttribute(field.value || "")}" placeholder="Valor">
        <button class="icon-button ghost" type="button" data-remove-product-field title="Remover campo" aria-label="Remover campo"><i data-lucide="x"></i></button>
      </div>
    `).join("");
  }

  async function renderEmployeesAdmin() {
    await loadEmployees();
    const draft = state.drafts.employee || {};
    elements.content.innerHTML = `
      <section class="two-columns">
        <form class="panel form-grid" data-employee-form>
          <input type="hidden" name="id" value="${escapeAttribute(draft.id || "")}">
          ${input("Nome", "name", draft.name || "", true)}
          ${input("Matrícula", "registration", draft.registration || "", true)}
          ${input("Seção", "section", draft.section || "")}
          <div class="form-actions">
            <button class="button primary" type="submit">${draft.id ? "Salvar funcionário" : "Criar funcionário"}</button>
            <button class="button ghost" type="button" data-clear-employee>Limpar</button>
          </div>
        </form>
        <section class="table-card">
          <table>
            <thead><tr><th>Nome</th><th>Matrícula</th><th>Seção</th><th></th></tr></thead>
            <tbody>
              ${state.employees.map((employee) => `
                <tr>
                  <td>${escapeHtml(employee.name)}</td>
                  <td>${escapeHtml(employee.registration)}</td>
                  <td>${escapeHtml(employee.section)}</td>
                  <td class="actions">
                    <button class="button secondary" type="button" data-edit-employee="${employee.id}">Editar</button>
                    <button class="button danger" type="button" data-delete-employee="${employee.id}">Remover</button>
                  </td>
                </tr>
              `).join("")}
            </tbody>
          </table>
        </section>
      </section>
    `;
    refreshIcons();
  }

  async function renderReminders() {
    await Promise.all([loadProducts(), loadReminders(), loadReminderDeliveryStatus(), loadPowerAutomateSettings()]);
    const draft = state.drafts.reminder || {};
    elements.content.innerHTML = `
      ${renderReminderDeliveryStatus()}
      ${renderPowerAutomateSettings()}
      <section class="two-columns">
        <form class="panel form-grid" data-reminder-form>
          <input type="hidden" name="id" value="${escapeAttribute(draft.id || "")}">
          ${input("Nome", "name", draft.name || "Alerta de estoque baixo", true)}
          <label><span>Ativo</span><select name="isActive"><option value="true" ${draft.isActive !== false ? "selected" : ""}>Sim</option><option value="false" ${draft.isActive === false ? "selected" : ""}>Não</option></select></label>
          ${input("Horário diário", "dailyTime", draft.dailyTime || "08:00", true, "time")}
          ${input("Destinatários", "recipients", draft.recipients || "", true)}
          ${input("Assunto", "subject", draft.subject || "Alerta de Estoque Baixo", true)}
          <label><span>Mensagem</span><textarea name="messageTemplate">${escapeHtml(draft.messageTemplate || "Estoque baixo\n\n{Products}\n\nGerado em: {GeneratedAt}")}</textarea></label>
          <label><span>Limite</span><select name="useProductMinimum"><option value="true" ${draft.useProductMinimum !== false ? "selected" : ""}>Estoque minimo do produto</option><option value="false" ${draft.useProductMinimum === false ? "selected" : ""}>Limite unico abaixo</option></select></label>
          ${input("Limite unico", "thresholdQuantity", draft.thresholdQuantity ?? "", false, "number")}
          <label><span>Itens incluídos nesta regra</span><textarea name="productCodesCsv" placeholder="Vazio = todos no alerta diário">${escapeHtml(draft.productCodesCsv || "")}</textarea></label>
          <label><span>Disparar ao movimentar</span><select name="triggerOnMovement"><option value="true" ${draft.triggerOnMovement !== false ? "selected" : ""}>Sim, somente o item movimentado</option><option value="false" ${draft.triggerOnMovement === false ? "selected" : ""}>Não, somente no horário diário</option></select></label>
          <input type="hidden" name="includeProductImages" value="false">
          <input type="hidden" name="maxPhotoAttachments" value="0">
          <div class="form-actions">
            <button class="button primary" type="submit">${draft.id ? "Salvar lembrete" : "Criar lembrete"}</button>
            <button class="button ghost" type="button" data-clear-reminder>Limpar</button>
          </div>
        </form>
        <section class="table-card">
          ${renderRemindersTable()}
        </section>
      </section>
    `;
    refreshIcons();
  }

  async function loadReminders() {
    state.reminders = await json("/api/reminders");
  }

  async function loadReminderDeliveryStatus() {
    state.reminderDelivery = await json("/api/reminders/delivery-status");
  }

  async function loadPowerAutomateSettings() {
    state.powerAutomateSettings = await json("/api/reminders/power-automate/settings");
  }

  function renderReminderDeliveryStatus() {
    const delivery = state.reminderDelivery || {};
    const ready = Boolean(delivery.powerAutomateConfigured || delivery.smtpConfigured);
    const title = delivery.powerAutomateConfigured
      ? "Power Automate ativo"
      : delivery.smtpConfigured
      ? "Envio SMTP ativo"
      : "Envio de e-mail inativo";
    return `
      <section class="status-banner ${ready ? "ready" : "warning"}">
        <div>
          <strong>${title}</strong>
          <span>${escapeHtml(delivery.message || "")}</span>
          <div class="status-chips">
            ${statusChip("Diário", delivery.powerAutomateDailyConfigured)}
            ${statusChip("Movimentação", delivery.powerAutomateMovementConfigured)}
            ${statusChip("Teste", delivery.powerAutomateManualConfigured)}
          </div>
          ${!ready && delivery.fallbackPath ? `<code>${escapeHtml(delivery.fallbackPath)}</code>` : ""}
        </div>
      </section>
    `;
  }

  function statusChip(label, active) {
    return `<span class="tag ${active ? "good" : "warn"}">${escapeHtml(label)}: ${active ? "Flow" : "fallback"}</span>`;
  }

  function renderPowerAutomateSettings() {
    const settings = state.powerAutomateSettings || {};
    return `
      <form class="panel form-grid two integration-panel" data-power-automate-settings-form>
        <div class="panel-heading">
          <h2>Power Automate</h2>
          <div class="status-chips">
            ${settingsChip("Diário", settings.dailyConfigured, settings.dailySource)}
            ${settingsChip("Movimentação", settings.movementConfigured, settings.movementSource)}
            ${settingsChip("Teste", settings.manualConfigured, settings.manualSource)}
            ${settingsChip("Segredo", settings.sharedSecretConfigured, settings.sharedSecretConfigured ? "ativo" : "")}
          </div>
        </div>
        ${secretInput("URL diária", "dailyWebhookUrl", settings.dailyWebhookUrlPreview)}
        ${secretInput("URL de movimentação", "movementWebhookUrl", settings.movementWebhookUrlPreview)}
        ${secretInput("URL teste manual", "manualWebhookUrl", settings.manualWebhookUrlPreview)}
        ${secretInput("Segredo compartilhado", "sharedSecret", settings.sharedSecretConfigured ? "Configurado" : "", "password")}
        <label class="checkbox-line"><input type="checkbox" name="clearDailyWebhookUrl"> Limpar diário</label>
        <label class="checkbox-line"><input type="checkbox" name="clearMovementWebhookUrl"> Limpar movimentação</label>
        <label class="checkbox-line"><input type="checkbox" name="clearManualWebhookUrl"> Limpar teste</label>
        <label class="checkbox-line"><input type="checkbox" name="clearSharedSecret"> Limpar segredo</label>
        <div class="form-actions">
          <button class="button primary" type="submit">Salvar webhooks</button>
        </div>
      </form>
    `;
  }

  function settingsChip(label, active, source = "") {
    const detail = active ? sourceLabel(source) : "vazio";
    return `<span class="tag ${active ? "good" : "warn"}">${escapeHtml(label)}: ${escapeHtml(detail)}</span>`;
  }

  function sourceLabel(source) {
    if (source === "site") return "site";
    if (source === "environment") return "ambiente";
    if (source) return source;
    return "ativo";
  }

  function secretInput(label, name, preview = "", type = "url") {
    const placeholder = preview ? `${preview} | cole novo valor para trocar` : "Cole o valor";
    return `
      <label>
        <span>${escapeHtml(label)}</span>
        <input name="${escapeAttribute(name)}" type="${escapeAttribute(type)}" value="" placeholder="${escapeAttribute(placeholder)}" autocomplete="off">
      </label>
    `;
  }

  function renderRemindersTable() {
    return `
      <table>
        <thead><tr><th>Nome</th><th>Horário</th><th>Destinatários</th><th>Status</th><th></th></tr></thead>
        <tbody>
          ${state.reminders.map((rule) => `
            <tr>
              <td>${escapeHtml(rule.name)}</td>
              <td>${escapeHtml(rule.dailyTime)}</td>
              <td>${escapeHtml(rule.recipients)}</td>
              <td>
                <span class="tag ${rule.isActive ? "good" : "warn"}">${rule.isActive ? "Ativo" : "Pausado"}</span>
                ${rule.includeProductImages ? `<span class="tag good">Fotos: ${Number(rule.maxPhotoAttachments || 3)}</span>` : ""}
              </td>
              <td class="actions">
                <button class="button secondary" type="button" data-edit-reminder="${rule.id}">Editar</button>
                <button class="button secondary" type="button" data-duplicate-reminder="${rule.id}">Duplicar</button>
                <button class="button ghost" type="button" data-test-reminder="${rule.id}">Testar</button>
                <button class="button danger" type="button" data-delete-reminder="${rule.id}">Remover</button>
              </td>
            </tr>
          `).join("")}
        </tbody>
      </table>
    `;
  }

  async function renderAccess() {
    const [catalog, users, approvals, approvalFlowSettings] = await Promise.all([
      accessJson("/catalog"),
      accessJson("/users"),
      accessJson("/approvals"),
      accessJson("/approval-flow/settings")
    ]);
    state.access = { catalog, users, approvals, approvalFlowSettings };
    const assignableRoles = rolesAssignableByCurrentUser(catalog.roles);
    const roleOptions = assignableRoles.map((role) => `<option value="${escapeAttribute(role.name)}">${escapeHtml(role.label)}</option>`).join("");

    elements.content.innerHTML = `
      <section class="two-columns">
        <form class="panel form-grid" data-invite-form>
          ${input("Nome", "name", "", true)}
          ${input("E-mail", "email", "", true, "email")}
          <label><span>Perfil</span><select name="role">${roleOptions}</select></label>
          <button class="button primary" type="submit">Enviar convite</button>
        </form>
        <section class="panel stack">
          <h2>Aprovações</h2>
          ${approvals.length ? approvals.map(renderApproval).join("") : empty("Nenhuma aprovação pendente.")}
        </section>
      </section>
      ${renderApprovalFlowSettings(approvalFlowSettings)}
      ${isDeveloperUser() ? renderScreenVisibilitySettings() : ""}
      <section class="table-card">
        ${renderUsersTable(users, assignableRoles)}
      </section>
      <section class="panel data-transfer-panel">
        <div>
          <h2>Banco de dados</h2>
          <p>Exporte ou importe todos os dados em um arquivo ZIP.</p>
        </div>
        <div class="form-actions">
          <button class="button secondary" type="button" data-export-database>Exportar banco</button>
          <button class="button ghost" type="button" data-import-database>Importar banco</button>
          <input class="hidden" type="file" accept=".zip,application/zip" data-import-file>
        </div>
      </section>
    `;
    refreshIcons();
  }

  function renderApprovalFlowSettings(settings) {
    const current = settings || {};
    const status = current.configured
      ? `<span class="tag good">Flow: ${escapeHtml(sourceLabel(current.source))}</span>`
      : `<span class="tag warn">Flow: fallback</span>`;
    const preview = current.webhookUrlPreview || "Nenhuma URL configurada";
    return `
      <form class="panel form-grid two integration-panel" data-approval-flow-settings-form>
        <div class="panel-heading">
          <h2>Solicitação de acesso</h2>
          <div class="status-chips">${status}</div>
        </div>
        ${secretInput("URL do Flow de aprovação", "webhookUrl", preview)}
        <label class="checkbox-line"><input type="checkbox" name="clearWebhookUrl"> Limpar URL do site</label>
        <div class="form-actions">
          <button class="button primary" type="submit">Salvar webhook</button>
        </div>
      </form>
    `;
  }

  function renderApproval(user) {
    const options = state.access.catalog.roles
      .filter((role) => role.name !== "admin")
      .filter((role) => role.name !== "developer" || isDeveloperUser())
      .map((role) => `<option value="${escapeAttribute(role.name)}">${escapeHtml(role.label)}</option>`)
      .join("");
    return `
      <div class="panel">
        <strong>${escapeHtml(user.name)}</strong>
        <p>${escapeHtml(user.email)}</p>
        <div class="form-actions">
          <select data-approval-role="${escapeAttribute(user.id)}">${options}</select>
          <button class="button secondary" type="button" data-approve-user="${escapeAttribute(user.id)}">Aprovar</button>
          <button class="button danger" type="button" data-reject-user="${escapeAttribute(user.id)}">Recusar</button>
        </div>
      </div>
    `;
  }

  function renderUsersTable(users, roles) {
    return `
      <table>
        <thead><tr><th>Nome</th><th>E-mail</th><th>Status</th><th>Perfil</th><th></th></tr></thead>
        <tbody>
          ${users.map((user) => `
            <tr>
              <td>${escapeHtml(user.name)}</td>
              <td>${escapeHtml(user.email)}</td>
              <td><span class="tag ${user.status === "approved" ? "good" : "warn"}">${escapeHtml(user.status)}</span></td>
              <td>
                <select data-role-for="${escapeAttribute(user.id)}">
                  ${rolesForUser(user, roles).map((role) => `<option value="${escapeAttribute(role.name)}" ${role.name === user.role ? "selected" : ""}>${escapeHtml(role.label)}</option>`).join("")}
                </select>
              </td>
              <td class="actions">
                <button class="button secondary" type="button" data-apply-role="${escapeAttribute(user.id)}">Aplicar</button>
                <button class="button ghost" type="button" data-password-link="${escapeAttribute(user.id)}">Link</button>
                <button class="button danger" type="button" data-remove-user="${escapeAttribute(user.id)}">Remover</button>
              </td>
            </tr>
          `).join("")}
        </tbody>
      </table>
    `;
  }

  function rolesAssignableByCurrentUser(roles) {
    return roles.filter((role) => role.name !== "developer" || isDeveloperUser());
  }

  function rolesForUser(user, roles) {
    if (roles.some((role) => role.name === user.role)) {
      return roles;
    }

    const currentRole = state.access.catalog?.roles?.find((role) => role.name === user.role);
    return currentRole ? [currentRole, ...roles] : roles;
  }

  function isDeveloperUser() {
    return state.auth?.user?.role === "developer";
  }

  function isScreenHiddenForCurrentRole(viewId) {
    if (isDeveloperUser()) {
      return false;
    }

    const role = state.auth?.user?.role || "";
    const hidden = state.screenVisibility?.hiddenScreensByRole?.[role] || [];
    return hidden.includes(viewId);
  }

  function renderDocumentation() {
    elements.content.innerHTML = state.language === "en" ? `
      <section class="docs-grid">
        <article class="panel doc-card">
          <i data-lucide="rocket"></i>
          <h2>Starting the service</h2>
          <p>Use <code>iniciar-servidor.ps1</code> to start the local server and generate a temporary public Cloudflare link when the tunnel tool is available.</p>
        </article>
        <article class="panel doc-card">
          <i data-lucide="globe-2"></i>
          <h2>Fixed domain</h2>
          <p>For a permanent address, configure a fixed Cloudflare Tunnel and point your DNS record to it. The step-by-step reference is in <code>DOMINIO_FIXO.md</code>.</p>
        </article>
        <article class="panel doc-card">
          <i data-lucide="shield-check"></i>
          <h2>Access and security</h2>
          <p>The Developer account is above Admin and can access technical catalog import tools. Default test login: <code>dev@email.com</code>. Change temporary passwords before production use.</p>
        </article>
        <article class="panel doc-card">
          <i data-lucide="file-spreadsheet"></i>
          <h2>Developer import</h2>
          <p>Developer users can upload XLSX, CSV, or searchable PDF catalogs, preview detected columns, choose the fields to import, and create or update items.</p>
        </article>
        <article class="panel doc-card">
          <i data-lucide="clipboard-list"></i>
          <h2>Next update task</h2>
          <p>Screen visibility for Admin and User is now controlled by Developer. The next task is an editable Developer screen for configuring visible fields and custom field behavior by access level.</p>
        </article>
      </section>
      <section class="panel legal-panel">
        <h2>Service ownership</h2>
        <p>Developed by: <strong>Davi Kasmirski dos Santos</strong>.</p>
        <p>Contact: <a href="mailto:luizds979@gmail.com">luizds979@gmail.com</a> · GitHub: <a href="https://github.com/DaviKdS" target="_blank" rel="noopener noreferrer">github.com/DaviKdS</a>.</p>
        <p>All rights reserved under applicable copyright law, including Brazilian Law No. 9,610/1998 where applicable. Unauthorized copying, resale, redistribution, or commercial exploitation of the service, layout, source code, brand elements, and documentation is prohibited without prior written authorization from the author.</p>
      </section>
    ` : `
      <section class="docs-grid">
        <article class="panel doc-card">
          <i data-lucide="rocket"></i>
          <h2>Iniciar o serviço</h2>
          <p>Use <code>iniciar-servidor.ps1</code> para iniciar o servidor local e gerar um link público temporário do Cloudflare quando a ferramenta de túnel estiver disponível.</p>
        </article>
        <article class="panel doc-card">
          <i data-lucide="globe-2"></i>
          <h2>Domínio fixo</h2>
          <p>Para ter um endereço permanente, configure um Cloudflare Tunnel fixo e aponte o registro DNS para ele. O passo a passo está em <code>DOMINIO_FIXO.md</code>.</p>
        </article>
        <article class="panel doc-card">
          <i data-lucide="shield-check"></i>
          <h2>Acesso e segurança</h2>
          <p>A conta Developer fica acima do Admin e pode acessar ferramentas técnicas de importação de catálogo. Login padrão de teste: <code>dev@email.com</code>. Troque senhas temporárias antes de produção.</p>
        </article>
        <article class="panel doc-card">
          <i data-lucide="file-spreadsheet"></i>
          <h2>Importação Developer</h2>
          <p>Usuários Developer podem enviar catálogos XLSX, CSV ou PDF pesquisável, visualizar colunas detectadas, escolher campos a importar e criar ou atualizar itens.</p>
        </article>
        <article class="panel doc-card">
          <i data-lucide="clipboard-list"></i>
          <h2>Tarefa da próxima atualização</h2>
          <p>A visibilidade de telas para Admin e User agora é controlada pelo Developer. A próxima tarefa é criar uma tela Developer editável para configurar campos visíveis e comportamento de campos personalizados por nível de acesso.</p>
        </article>
      </section>
      <section class="panel legal-panel">
        <h2>Titularidade do serviço</h2>
        <p>Desenvolvido por: <strong>Davi Kasmirski dos Santos</strong>.</p>
        <p>Contato: <a href="mailto:luizds979@gmail.com">luizds979@gmail.com</a> · GitHub: <a href="https://github.com/DaviKdS" target="_blank" rel="noopener noreferrer">github.com/DaviKdS</a>.</p>
        <p>Todos os direitos reservados conforme a legislação de direitos autorais aplicável, incluindo a Lei nº 9.610/1998 no Brasil quando aplicável. É proibida a cópia, revenda, redistribuição ou exploração comercial não autorizada do serviço, layout, código-fonte, elementos de marca e documentação sem autorização prévia e por escrito do autor.</p>
      </section>
    `;
    refreshIcons();
  }

  function renderScreenVisibilitySettings() {
    const configurableViews = views.filter((view) => view.id !== "access");
    return `
      <form class="panel screen-visibility-panel" data-screen-visibility-form>
        <div class="panel-heading">
          <div>
            <h2>Visibilidade por perfil</h2>
            <p>Marque as telas que devem ficar ocultas para Admin ou User. Developer sempre enxerga tudo.</p>
          </div>
          <span class="tag good">Developer</span>
        </div>
        <div class="visibility-grid">
          ${screenVisibilityRoles.map((role) => `
            <section>
              <h3>${escapeHtml(role.label)}</h3>
              ${configurableViews.map((view) => `
                <label class="checkbox-line">
                  <input type="checkbox" name="${escapeAttribute(role.role)}" value="${escapeAttribute(view.id)}" ${isScreenHiddenForRole(role.role, view.id) ? "checked" : ""}>
                  ${escapeHtml(viewText(view, "label"))}
                </label>
              `).join("")}
            </section>
          `).join("")}
        </div>
        <div class="form-actions">
          <button class="button primary" type="submit"><i data-lucide="save"></i><span>Salvar visibilidade</span></button>
        </div>
      </form>
    `;
  }

  function isScreenHiddenForRole(role, viewId) {
    const hidden = state.screenVisibility?.hiddenScreensByRole?.[role] || [];
    return hidden.includes(viewId);
  }

  function renderReleases() {
    elements.content.innerHTML = `
      <section class="release-list">
        <article class="panel release-card">
          <div>
            <span class="tag warn">v1.2.2-beta</span>
            <h2>${state.language === "en" ? "Automatic access refresh" : "Atualização automática de acessos"}</h2>
            <p>${state.language === "en"
              ? "Applies role and screen visibility changes automatically on desktop and mobile sessions, renewing permissions without requiring a new login when access changes."
              : "Aplica mudanças de perfil e visibilidade de telas automaticamente em sessões desktop e mobile, renovando permissões sem exigir novo login quando o acesso muda."}</p>
          </div>
        </article>
        <article class="panel release-card">
          <div>
            <span class="tag warn">v1.2.0-beta</span>
            <h2>${state.language === "en" ? "Developer access and catalog import test version" : "Versão de testes com acesso Developer e importação de catálogo"}</h2>
            <p>${state.language === "en"
              ? "Adds the Developer role above Admin, the default Developer test login, mobile menu adjustments, Developer-only catalog import for XLSX/CSV/searchable PDF, screen visibility controls for Admin/User, internal documentation updates, and the next task for editable Dev fields."
              : "Adiciona o perfil Developer acima do Admin, login Developer padrão de teste, ajuste do menu mobile, importação de catálogo apenas para Developer por XLSX/CSV/PDF pesquisável, controle de visibilidade de telas para Admin/User, documentação interna e próxima tarefa para campos editáveis no modo Dev."}</p>
          </div>
        </article>
        <article class="panel release-card">
          <div>
            <span class="tag good">v1.1.0</span>
            <h2>${state.language === "en" ? "English support and visual improvements" : "Suporte em inglês e melhorias visuais"}</h2>
            <p>${state.language === "en"
              ? "Adds PT/EN interface navigation, documentation inside the app, release notes, visual refinements, image upload improvements, and service copyright/contact information."
              : "Adiciona navegação PT/EN, documentação dentro do app, notas de versão, refinamentos visuais, melhorias no anexo de imagens e informações de direitos autorais/contato do serviço."}</p>
          </div>
        </article>
        <article class="panel release-card">
          <div>
            <span class="tag">v1.0.0</span>
            <h2>${state.language === "en" ? "Initial stable version" : "Versão estável inicial"}</h2>
            <p>${state.language === "en"
              ? "Base inventory control with catalog, stock movements, employees, reminders, access control, PWA files, and temporary public link helper."
              : "Base do controle de estoque com catálogo, movimentações, funcionários, lembretes, controle de acesso, arquivos PWA e auxiliar de link público temporário."}</p>
          </div>
        </article>
      </section>
    `;
  }

  function renderFooter() {
    if (!elements.siteFooter) return;

    const year = new Date().getFullYear();
    elements.siteFooter.innerHTML = state.language === "en"
      ? `Developed by: <strong>Davi Kasmirski dos Santos</strong>. Contact: <a href="mailto:luizds979@gmail.com">luizds979@gmail.com</a> · <a href="https://github.com/DaviKdS" target="_blank" rel="noopener noreferrer">GitHub</a>. © ${year} Davi Kasmirski dos Santos. All rights reserved.`
      : `Desenvolvido por: <strong>Davi Kasmirski dos Santos</strong>. Contato: <a href="mailto:luizds979@gmail.com">luizds979@gmail.com</a> · <a href="https://github.com/DaviKdS" target="_blank" rel="noopener noreferrer">GitHub</a>. © ${year} Davi Kasmirski dos Santos. Todos os direitos reservados.`;
  }

  async function handleContentClick(event) {
    const target = event.target.closest("button");
    if (!target) return;

    try {
      if (target.dataset.stockOut) {
        state.view = "out";
        await loadCurrentView();
        selectProduct(target.dataset.stockOut);
      } else if (target.dataset.stockIn) {
        state.view = "in";
        await loadCurrentView();
        selectProduct(target.dataset.stockIn);
      } else if (target.dataset.criticalOnly !== undefined) {
        await renderStock("", true);
      } else if (target.dataset.editProduct) {
        state.drafts.product = state.products.find((product) => product.code === target.dataset.editProduct);
        await renderProductsAdmin();
      } else if (target.dataset.deleteProduct) {
        if (confirm("Remover produto?")) {
          await json(`/api/products/${encodeURIComponent(target.dataset.deleteProduct)}`, { method: "DELETE" });
          await renderProductsAdmin();
        }
      } else if (target.dataset.clearProduct !== undefined) {
        state.drafts.product = null;
        await renderProductsAdmin();
      } else if (target.dataset.addProductField !== undefined) {
        addProductField();
      } else if (target.dataset.removeProductField !== undefined) {
        removeProductField(target);
      } else if (target.dataset.productImagePick !== undefined) {
        target.closest("form")?.querySelector("[data-product-image-input]")?.click();
      } else if (target.dataset.editEmployee) {
        state.drafts.employee = state.employees.find((employee) => String(employee.id) === target.dataset.editEmployee);
        await renderEmployeesAdmin();
      } else if (target.dataset.deleteEmployee) {
        if (confirm("Remover funcionário?")) {
          await json(`/api/employees/${target.dataset.deleteEmployee}`, { method: "DELETE" });
          await renderEmployeesAdmin();
        }
      } else if (target.dataset.clearEmployee !== undefined) {
        state.drafts.employee = null;
        await renderEmployeesAdmin();
      } else if (target.dataset.editReminder) {
        state.drafts.reminder = state.reminders.find((rule) => String(rule.id) === target.dataset.editReminder);
        await renderReminders();
      } else if (target.dataset.duplicateReminder) {
        await json(`/api/reminders/${target.dataset.duplicateReminder}/duplicate`, { method: "POST" });
        showToast("Lembrete duplicado.", "success");
        await renderReminders();
      } else if (target.dataset.testReminder) {
        const result = await json(`/api/reminders/${target.dataset.testReminder}/test`, { method: "POST" });
        showToast(result.message || "Lembrete testado.", result.sent ? "success" : result.logged ? "warning" : "error");
      } else if (target.dataset.deleteReminder) {
        if (confirm("Remover lembrete?")) {
          await json(`/api/reminders/${target.dataset.deleteReminder}`, { method: "DELETE" });
          await renderReminders();
        }
      } else if (target.dataset.clearReminder !== undefined) {
        state.drafts.reminder = null;
        await renderReminders();
      } else if (target.dataset.approveUser) {
        const role = elements.content.querySelector(`[data-approval-role="${target.dataset.approveUser}"]`)?.value || "standard";
        await accessJson(`/approvals/${target.dataset.approveUser}/approve`, { method: "POST", body: JSON.stringify({ role }) });
        await renderAccess();
      } else if (target.dataset.rejectUser) {
        await accessJson(`/approvals/${target.dataset.rejectUser}/reject`, { method: "POST", body: JSON.stringify({ reason: "Recusado pelo administrador." }) });
        await renderAccess();
      } else if (target.dataset.applyRole) {
        const role = elements.content.querySelector(`[data-role-for="${target.dataset.applyRole}"]`)?.value || "standard";
        await accessJson(`/users/${target.dataset.applyRole}/role`, { method: "PUT", body: JSON.stringify({ role }) });
        await renderAccess();
      } else if (target.dataset.passwordLink) {
        await accessJson(`/users/${target.dataset.passwordLink}/password-link`, { method: "POST" });
        showToast("Link de senha enviado.", "success");
      } else if (target.dataset.removeUser) {
        if (confirm("Remover acesso?")) {
          await accessJson(`/users/${target.dataset.removeUser}`, { method: "DELETE" });
          await renderAccess();
        }
      } else if (target.dataset.exportDatabase !== undefined) {
        const response = await fetch("/api/access/database/export", { credentials: "same-origin" });
        if (!response.ok) {
          let message = "Não foi possível exportar o banco de dados.";
          try {
            message = (await response.json()).message || message;
          } catch {
            // Mantém a mensagem genérica quando a resposta não for JSON.
          }
          throw new Error(message);
        }
        const blob = await response.blob();
        const url = URL.createObjectURL(blob);
        const link = document.createElement("a");
        link.href = url;
        link.download = "generic-inventory-backup.zip";
        link.click();
        URL.revokeObjectURL(url);
        showToast("Backup exportado.", "success");
      } else if (target.dataset.importDatabase !== undefined) {
        elements.content.querySelector("[data-import-file]")?.click();
      }
    } catch (error) {
      showError(error);
    }
  }

  async function handleContentSubmit(event) {
    event.preventDefault();
    const form = event.target;

    try {
      if (form.dataset.movementForm) {
        await saveMovement(form);
      } else if (form.dataset.productForm !== undefined) {
        await saveProduct(form);
      } else if (form.dataset.catalogPreviewForm !== undefined) {
        await previewCatalogImport(form);
      } else if (form.dataset.catalogImportForm !== undefined) {
        await importCatalog(form);
      } else if (form.dataset.employeeForm !== undefined) {
        await saveEmployee(form);
      } else if (form.dataset.reminderForm !== undefined) {
        await saveReminder(form);
      } else if (form.dataset.powerAutomateSettingsForm !== undefined) {
        await savePowerAutomateSettings(form);
      } else if (form.dataset.approvalFlowSettingsForm !== undefined) {
        await saveApprovalFlowSettings(form);
      } else if (form.dataset.screenVisibilityForm !== undefined) {
        await saveScreenVisibility(form);
      } else if (form.dataset.inviteForm !== undefined) {
        await inviteUser(form);
      }
    } catch (error) {
      showError(error);
    }
  }

  function handleContentInput(event) {
    if (event.target.id === "stockSearch") {
      clearTimeout(state.searchTimer);
      state.searchTimer = setTimeout(() => renderStock(event.target.value).catch(showError), 250);
    }
  }

  async function handleContentChange(event) {
    const productImageInput = event.target.closest("[data-product-image-input]");
    if (productImageInput && productImageInput.files.length) {
      try {
        await uploadProductImage(productImageInput.files[0], productImageInput.closest("form"));
      } catch (error) {
        showError(error);
      } finally {
        productImageInput.value = "";
      }
      return;
    }

    const input = event.target.closest("[data-import-file]");
    if (!input || !input.files.length) return;

    try {
      const formData = new FormData();
      formData.append("file", input.files[0]);
      const result = await accessJson("/database/import", { method: "POST", body: formData });
      showToast(result.message || "Banco importado.", "success");
      await refreshSession();
    } catch (error) {
      showError(error);
    } finally {
      input.value = "";
    }
  }

  async function saveMovement(form) {
    const data = new FormData(form);
    const kind = form.dataset.movementForm;
    const endpoint = kind === "out" ? "/api/movements/stock-out" : "/api/movements/stock-in";
    await json(endpoint, {
      method: "POST",
      body: JSON.stringify({
        productCode: String(data.get("productCode") || ""),
        quantity: Number(data.get("quantity") || 0),
        employeeId: data.get("employeeId") ? Number(data.get("employeeId")) : null
      })
    });
    showToast("Movimento registrado.", "success");
    form.reset();
  }

  async function saveProduct(form) {
    const data = new FormData(form);
    const originalCode = String(data.get("originalCode") || "");
    const imagePath = await uploadProductImageIfNeeded(form, data);
    const payload = formToObject(data, ["code", "description", "legacyImageUrl"], ["currentStock", "minimumStock", "saleValue"]);
    payload.imagePath = imagePath;
    payload.customFields = collectCustomFields(form);
    const path = originalCode ? `/api/products/${encodeURIComponent(originalCode)}` : "/api/products";
    await json(path, { method: originalCode ? "PUT" : "POST", body: JSON.stringify(payload) });
    state.drafts.product = null;
    showToast("Produto salvo.", "success");
    await renderProductsAdmin();
  }

  async function previewCatalogImport(form) {
    const file = form.querySelector('input[name="file"]')?.files?.[0];
    if (!file) {
      throw new Error("Selecione um arquivo de catálogo.");
    }

    const data = new FormData();
    data.append("file", file);
    state.catalogImportPreview = await json("/api/products/catalog/preview", { method: "POST", body: data });
    await renderProductsAdmin();
    showToast(state.catalogImportPreview.message || "Prévia carregada.", "success");
  }

  async function importCatalog(form) {
    const file = form.querySelector('input[name="file"]')?.files?.[0];
    if (!file) {
      throw new Error("Reenvie o arquivo para confirmar a importação.");
    }

    const data = new FormData(form);
    const mapping = {
      codeColumn: String(data.get("codeColumn") || ""),
      descriptionColumn: String(data.get("descriptionColumn") || ""),
      currentStockColumn: String(data.get("currentStockColumn") || ""),
      minimumStockColumn: String(data.get("minimumStockColumn") || ""),
      saleValueColumn: String(data.get("saleValueColumn") || ""),
      imagePathColumn: String(data.get("imagePathColumn") || ""),
      customColumns: data.getAll("customColumns").map((value) => String(value))
    };

    const upload = new FormData();
    upload.append("file", file);
    upload.append("mapping", JSON.stringify(mapping));
    const result = await json("/api/products/catalog/import", { method: "POST", body: upload });
    state.catalogImportPreview = null;
    state.drafts.product = null;
    await renderProductsAdmin();
    showToast(`Importação concluída: ${result.created || 0} criado(s), ${result.updated || 0} atualizado(s).`, "success");
  }

  async function saveEmployee(form) {
    const data = new FormData(form);
    const id = String(data.get("id") || "");
    const payload = formToObject(data, ["name", "registration", "section"], []);
    await json(id ? `/api/employees/${id}` : "/api/employees", { method: id ? "PUT" : "POST", body: JSON.stringify(payload) });
    state.drafts.employee = null;
    showToast("Funcionário salvo.", "success");
    await renderEmployeesAdmin();
  }

  function handleContentDragOver(event) {
    const dropzone = event.target.closest("[data-product-image-drop]");
    if (!dropzone) return;

    event.preventDefault();
    dropzone.classList.add("dragging");
  }

  function handleContentDragLeave(event) {
    const dropzone = event.target.closest("[data-product-image-drop]");
    if (!dropzone || dropzone.contains(event.relatedTarget)) return;

    dropzone.classList.remove("dragging");
  }

  async function handleContentDrop(event) {
    const dropzone = event.target.closest("[data-product-image-drop]");
    if (!dropzone) return;

    event.preventDefault();
    dropzone.classList.remove("dragging");
    const file = event.dataTransfer?.files?.[0];
    if (!file) return;

    try {
      await uploadProductImage(file, dropzone.closest("form"));
    } catch (error) {
      showError(error);
    }
  }

  async function uploadProductImageIfNeeded(form, data) {
    const file = form.querySelector('input[name="imageFile"]')?.files?.[0];
    if (!file) {
      return String(data.get("imagePath") || "");
    }

    const upload = new FormData();
    upload.append("file", file);
    const result = await json("/api/products/image", { method: "POST", body: upload });
    return result.imagePath || "";
  }

  async function uploadProductImage(file, form) {
    if (!file.type.startsWith("image/")) {
      throw new Error("Envie um arquivo de imagem.");
    }

    const upload = new FormData();
    upload.append("file", file);
    const result = await json("/api/products/image", { method: "POST", body: upload });
    const imagePath = String(result.imagePath || "");
    const imagePathInput = form?.querySelector('input[name="imagePath"]');
    const preview = form?.querySelector("[data-product-image-preview]");
    const status = form?.querySelector("[data-product-image-status]");

    if (imagePathInput) imagePathInput.value = imagePath;
    const legacyImageInput = form?.querySelector('input[name="legacyImageUrl"]');
    if (legacyImageInput) legacyImageInput.value = "";
    if (preview) preview.innerHTML = `<img src="${escapeAttribute(imagePath)}" alt="">`;
    if (status) status.textContent = "Imagem importada.";
    showToast("Imagem importada.", "success");
  }

  function collectCustomFields(form) {
    const names = [...form.querySelectorAll('input[name="customFieldName"]')];
    const values = [...form.querySelectorAll('input[name="customFieldValue"]')];
    return names.map((input, index) => ({
      name: input.value.trim(),
      value: values[index]?.value.trim() || ""
    })).filter((field) => field.name || field.value);
  }

  function addProductField() {
    const list = elements.content.querySelector(".custom-field-list");
    if (!list) return;
    list.insertAdjacentHTML("beforeend", renderCustomFieldInputs([{ name: "", value: "" }]));
  }

  function removeProductField(button) {
    const row = button.closest(".custom-field-row");
    const list = button.closest(".custom-field-list");
    if (!row || !list) return;

    if (list.querySelectorAll(".custom-field-row").length <= 1) {
      row.querySelectorAll("input").forEach((input) => input.value = "");
      return;
    }

    row.remove();
  }

  async function saveReminder(form) {
    const data = new FormData(form);
    const id = String(data.get("id") || "");
    const payload = formToObject(data, ["name", "dailyTime", "recipients", "subject", "messageTemplate", "productCodesCsv"], ["thresholdQuantity", "maxPhotoAttachments"]);
    payload.isActive = data.get("isActive") === "true";
    payload.useProductMinimum = data.get("useProductMinimum") === "true";
    payload.triggerOnMovement = data.get("triggerOnMovement") === "true";
    payload.includeProductImages = data.get("includeProductImages") === "true";
    if (payload.thresholdQuantity === 0 && !String(data.get("thresholdQuantity") || "").trim()) payload.thresholdQuantity = null;
    if (!payload.maxPhotoAttachments) payload.maxPhotoAttachments = 3;
    await json(id ? `/api/reminders/${id}` : "/api/reminders", { method: id ? "PUT" : "POST", body: JSON.stringify(payload) });
    state.drafts.reminder = null;
    showToast("Lembrete salvo.", "success");
    await renderReminders();
  }

  async function savePowerAutomateSettings(form) {
    const data = new FormData(form);
    await json("/api/reminders/power-automate/settings", {
      method: "PUT",
      body: JSON.stringify({
        dailyWebhookUrl: String(data.get("dailyWebhookUrl") || "").trim(),
        movementWebhookUrl: String(data.get("movementWebhookUrl") || "").trim(),
        manualWebhookUrl: String(data.get("manualWebhookUrl") || "").trim(),
        sharedSecret: String(data.get("sharedSecret") || "").trim(),
        clearDailyWebhookUrl: data.get("clearDailyWebhookUrl") === "on",
        clearMovementWebhookUrl: data.get("clearMovementWebhookUrl") === "on",
        clearManualWebhookUrl: data.get("clearManualWebhookUrl") === "on",
        clearSharedSecret: data.get("clearSharedSecret") === "on"
      })
    });
    showToast("Webhooks salvos.", "success");
    await renderReminders();
  }

  async function saveApprovalFlowSettings(form) {
    const data = new FormData(form);
    await accessJson("/approval-flow/settings", {
      method: "PUT",
      body: JSON.stringify({
        webhookUrl: String(data.get("webhookUrl") || "").trim(),
        clearWebhookUrl: data.get("clearWebhookUrl") === "on"
      })
    });
    showToast("Webhook de aprovação salvo.", "success");
    await renderAccess();
  }

  async function saveScreenVisibility(form) {
    const data = new FormData(form);
    const hiddenScreensByRole = {};
    screenVisibilityRoles.forEach((role) => {
      hiddenScreensByRole[role.role] = data.getAll(role.role).map((value) => String(value));
    });

    state.screenVisibility = await json("/api/access/screen-visibility", {
      method: "PUT",
      body: JSON.stringify({ hiddenScreensByRole })
    });

    renderNav();
    showToast("Visibilidade das telas atualizada.", "success");
    await renderAccess();
  }

  async function inviteUser(form) {
    const data = new FormData(form);
    await accessJson("/users", {
      method: "POST",
      body: JSON.stringify({
        name: String(data.get("name") || "").trim(),
        email: String(data.get("email") || "").trim(),
        role: String(data.get("role") || "standard")
      })
    });
    showToast("Convite enviado.", "success");
    form.reset();
    await renderAccess();
  }

  function selectProduct(code) {
    renderNav();
    const select = elements.content.querySelector('select[name="productCode"]');
    if (select) select.value = code;
  }

  function renderMovementTable(movements) {
    if (!movements.length) return empty("Nenhum movimento registrado.");
    return `
      <div class="table-card">
        <table>
          <thead><tr><th>Data</th><th>Tipo</th><th>Item</th><th>Qtd</th><th>Total</th><th>Funcionário</th></tr></thead>
          <tbody>
            ${movements.map((movement) => `
              <tr>
                <td>${formatDate(movement.date)}</td>
                <td><span class="tag ${movement.type === "Saida" || movement.type === "Saída" ? "warn" : "good"}">${escapeHtml(movement.type)}</span></td>
                <td>${escapeHtml(movement.productCode)} - ${escapeHtml(movement.productDescription)}</td>
                <td>${formatNumber(movement.quantity)}</td>
                <td>R$ ${formatMoney(movement.totalValue)}</td>
                <td>${escapeHtml(movement.employeeName || "")}</td>
              </tr>
            `).join("")}
          </tbody>
        </table>
      </div>
    `;
  }

  function metric(label, value, tone = "") {
    return `<div class="metric-card"><span>${escapeHtml(label)}</span><strong class="${tone}">${escapeHtml(String(value))}</strong></div>`;
  }

  function input(label, name, value = "", required = false, type = "text") {
    const step = type === "number" ? ' step="0.01"' : "";
    return `
      <label>
        <span>${escapeHtml(label)}</span>
        <input name="${escapeAttribute(name)}" type="${escapeAttribute(type)}" value="${escapeAttribute(value)}"${required ? " required" : ""}${step}>
      </label>
    `;
  }

  function productImageField(draft) {
    const image = draft.imagePath || draft.legacyImageUrl || "";
    return `
      <label class="image-field">
        <span>Imagem do item</span>
        <input type="hidden" name="imagePath" value="${escapeAttribute(draft.imagePath || "")}">
        <input type="hidden" name="legacyImageUrl" value="${escapeAttribute(draft.legacyImageUrl || "")}">
        <input class="hidden" type="file" accept="image/*" data-product-image-input>
        <div class="image-dropzone" data-product-image-drop>
          <div class="image-preview" data-product-image-preview>
            ${image ? `<img src="${escapeAttribute(image)}" alt="">` : `<i data-lucide="image-plus"></i>`}
          </div>
          <div>
            <strong data-product-image-status>${image ? "Imagem selecionada" : "Importar imagem"}</strong>
            <span>Arraste uma imagem aqui ou escolha do PC.</span>
          </div>
          <button class="button secondary" type="button" data-product-image-pick><i data-lucide="upload"></i><span>Escolher</span></button>
        </div>
      </label>
    `;
  }

  function formToObject(data, strings, numbers) {
    const output = {};
    strings.forEach((key) => output[key] = String(data.get(key) || "").trim());
    numbers.forEach((key) => output[key] = Number(data.get(key) || 0));
    return output;
  }

  function viewText(view, key) {
    const value = view?.[key];
    if (!value || typeof value === "string") return value || "";
    return value[state.language] || value.pt || value.en || "";
  }

  function can(permission) {
    if (!permission) return true;
    return Boolean(state.auth?.permissions?.includes(permission));
  }

  function showToast(message, type = "") {
    elements.toast.textContent = message;
    elements.toast.className = `toast ${type}`.trim();
    elements.toast.classList.remove("hidden");
    setTimeout(() => elements.toast.classList.add("hidden"), 3500);
  }

  function showError(error) {
    const message = error instanceof Error ? error.message : String(error);
    if (elements.authGate && !elements.authGate.classList.contains("hidden")) {
      setAuthMessage(message, "error");
      return;
    }
    showToast(message, "error");
  }

  function empty(message) {
    return `<div class="empty">${escapeHtml(message)}</div>`;
  }

  function formatNumber(value) {
    return Number(value || 0).toLocaleString(locale(), { maximumFractionDigits: 2 });
  }

  function formatMoney(value) {
    return Number(value || 0).toLocaleString(locale(), { minimumFractionDigits: 2, maximumFractionDigits: 2 });
  }

  function formatDate(value) {
    return value ? new Date(value).toLocaleDateString(locale()) : "";
  }

  function locale() {
    return state.language === "en" ? "en-US" : "pt-BR";
  }

  function normalizeHeader(value) {
    return String(value || "")
      .normalize("NFD")
      .replace(/[\u0300-\u036f]/g, "")
      .replace(/[^a-z0-9]/gi, "")
      .toUpperCase();
  }

  function productInitials(product) {
    const text = product.description || product.code || "Item";
    return text
      .split(/\s+/)
      .filter(Boolean)
      .slice(0, 2)
      .map((part) => part[0])
      .join("")
      .toUpperCase();
  }

  function escapeHtml(value) {
    return String(value ?? "")
      .replaceAll("&", "&amp;")
      .replaceAll("<", "&lt;")
      .replaceAll(">", "&gt;")
      .replaceAll('"', "&quot;")
      .replaceAll("'", "&#039;");
  }

  function escapeAttribute(value) {
    return escapeHtml(value);
  }

  function readStoredBoolean(key) {
    try {
      return localStorage.getItem(key) === "true";
    } catch {
      return false;
    }
  }

  function readStoredString(key, fallback = "") {
    try {
      return localStorage.getItem(key) || fallback;
    } catch {
      return fallback;
    }
  }

  function refreshIcons() {
    if (window.lucide) {
      window.lucide.createIcons();
    }
  }

  function registerServiceWorker() {
    if (!("serviceWorker" in navigator)) {
      return;
    }

    window.addEventListener("load", () => {
      navigator.serviceWorker.register("/service-worker.js").catch(() => {
        // Android can still use the responsive web app when service workers are unavailable.
      });
    });
  }
})();
