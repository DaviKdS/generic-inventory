(() => {
  const permissions = {
    accessManage: "access.manage",
    stockRead: "stock.read",
    stockMove: "stock.move",
    productsManage: "products.manage",
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
    access: { catalog: null, users: [], approvals: [], approvalFlowSettings: null },
    drafts: { product: null, employee: null, reminder: null },
    pendingPassword: null,
    sidebarCollapsed: readStoredBoolean("generic-inventory.sidebarCollapsed"),
    theme: document.documentElement.dataset.theme === "dark" ? "dark" : "light"
  };

  const views = [
    { id: "stock", label: "Catálogo", permission: permissions.stockRead, title: "Catálogo", subtitle: "Itens, imagens, estoque e informações personalizadas." },
    { id: "out", label: "Saída", permission: permissions.stockMove, title: "Saída", subtitle: "Registre baixa de estoque." },
    { id: "in", label: "Entrada", permission: permissions.stockMove, title: "Entrada", subtitle: "Registre reposição de estoque." },
    { id: "movements", label: "Movimentações", permission: permissions.stockRead, title: "Movimentações", subtitle: "Histórico de entradas e saídas." },
    { id: "products", label: "Itens", permission: permissions.productsManage, title: "Itens", subtitle: "Cadastro flexível do catálogo." },
    { id: "employees", label: "Funcionários", permission: permissions.employeesManage, title: "Funcionários", subtitle: "Pessoas disponíveis para movimentações." },
    { id: "reminders", label: "Lembretes", permission: permissions.remindersManage, title: "Lembretes", subtitle: "Regras editáveis de alerta de estoque." },
    { id: "access", label: "Acessos", permission: permissions.accessManage, title: "Acessos", subtitle: "Contas, convites e aprovações." }
  ];

  document.addEventListener("DOMContentLoaded", init);
  registerServiceWorker();

  async function init() {
    bindElements();
    setTheme(state.theme, false);
    bindEvents();
    readPasswordInvite();
    await refreshSession();
  }

  function bindElements() {
    [
      "authGate", "appShell", "authHint", "loginForm", "registerForm", "passwordForm",
      "authMessage", "navMenu", "sidebarToggle", "content", "viewTitle", "viewSubtitle", "userMenu", "toast"
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
      } else if (event.target.closest("[data-logout]")) {
        logout().catch(showError);
      }
    });

    elements.content.addEventListener("click", handleContentClick);
    elements.content.addEventListener("submit", handleContentSubmit);
    elements.content.addEventListener("input", handleContentInput);
    elements.content.addEventListener("change", handleContentChange);
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
    if (options.body && !headers["Content-Type"]) {
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
    applySidebarState();
    renderNav();
    renderUserMenu();
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
    const available = views.filter((view) => can(view.permission));
    if (!available.some((view) => view.id === state.view)) {
      state.view = available[0]?.id || "stock";
    }

    elements.navMenu.innerHTML = available.map((view, index) => `
      <button class="nav-item ${state.view === view.id ? "active" : ""}" type="button" data-view="${view.id}" title="${escapeAttribute(view.label)}" style="--entry-delay: ${index * 28}ms">
        <span>${escapeHtml(view.label)}</span>
      </button>
    `).join("");
  }

  function toggleSidebar() {
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
    elements.sidebarToggle.textContent = state.sidebarCollapsed ? "Abrir" : "Menu";
    elements.sidebarToggle.title = state.sidebarCollapsed ? "Expandir menu" : "Recolher menu";
    elements.sidebarToggle.setAttribute("aria-label", elements.sidebarToggle.title);
    elements.sidebarToggle.setAttribute("aria-expanded", String(!state.sidebarCollapsed));
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
      <button class="button ghost" type="button" data-logout>Sair</button>
    ` : "";
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
  }

  function renderThemeToggleButtons() {
    const label = themeToggleLabel();
    document.querySelectorAll("[data-theme-toggle]").forEach((button) => {
      button.title = label;
      button.setAttribute("aria-label", label);
      button.textContent = state.theme === "dark" ? "Tema claro" : "Tema escuro";
    });
  }

  function themeToggleLabel() {
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
    elements.viewTitle.textContent = view.title;
    elements.viewSubtitle.textContent = view.subtitle;

    if (state.view === "stock") return renderStock();
    if (state.view === "out") return renderMovementForm("out");
    if (state.view === "in") return renderMovementForm("in");
    if (state.view === "movements") return renderMovements();
    if (state.view === "products") return renderProductsAdmin();
    if (state.view === "employees") return renderEmployeesAdmin();
    if (state.view === "reminders") return renderReminders();
    if (state.view === "access") return renderAccess();
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
              <button class="button ghost" type="button" data-stock-out="${escapeAttribute(product.code)}">Saída</button>
              <button class="button secondary" type="button" data-stock-in="${escapeAttribute(product.code)}">Entrada</button>
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
          <button class="button primary" type="submit">Registrar ${typeLabel}</button>
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
          <input type="hidden" name="imagePath" value="${escapeAttribute(draft.imagePath || "")}">
          <label class="image-field">
            <span>Imagem do item</span>
            <input name="imageFile" type="file" accept="image/*">
            <small>${draft.imagePath ? "Imagem anexada." : "JPG, PNG, WEBP ou GIF."}</small>
          </label>
          <section class="custom-fields-editor">
            <div class="panel-heading">
              <h2>Campos personalizados</h2>
              <button class="button ghost" type="button" data-add-product-field>Adicionar campo</button>
            </div>
            <div class="custom-field-list">
              ${renderCustomFieldInputs(draft.customFields)}
            </div>
          </section>
          <div class="form-actions">
            <button class="button primary" type="submit">${draft.code ? "Salvar item" : "Criar item"}</button>
            <button class="button ghost" type="button" data-clear-product>Limpar</button>
          </div>
        </form>
        <section class="table-card">
          ${renderProductsTable()}
        </section>
      </section>
    `;
    refreshIcons();
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
                <button class="button secondary" type="button" data-edit-product="${escapeAttribute(product.code)}">Editar</button>
                <button class="button danger" type="button" data-delete-product="${escapeAttribute(product.code)}">Remover</button>
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
        <button class="button ghost" type="button" data-remove-product-field title="Remover campo" aria-label="Remover campo">Remover</button>
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
    const roleOptions = catalog.roles.map((role) => `<option value="${escapeAttribute(role.name)}">${escapeHtml(role.label)}</option>`).join("");

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
      <section class="table-card">
        ${renderUsersTable(users, catalog.roles)}
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
                  ${roles.map((role) => `<option value="${escapeAttribute(role.name)}" ${role.name === user.role ? "selected" : ""}>${escapeHtml(role.label)}</option>`).join("")}
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
      } else if (form.dataset.employeeForm !== undefined) {
        await saveEmployee(form);
      } else if (form.dataset.reminderForm !== undefined) {
        await saveReminder(form);
      } else if (form.dataset.powerAutomateSettingsForm !== undefined) {
        await savePowerAutomateSettings(form);
      } else if (form.dataset.approvalFlowSettingsForm !== undefined) {
        await saveApprovalFlowSettings(form);
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
    const payload = formToObject(data, ["code", "description"], ["currentStock", "minimumStock", "saleValue"]);
    payload.imagePath = imagePath;
    payload.customFields = collectCustomFields(form);
    const path = originalCode ? `/api/products/${encodeURIComponent(originalCode)}` : "/api/products";
    await json(path, { method: originalCode ? "PUT" : "POST", body: JSON.stringify(payload) });
    state.drafts.product = null;
    showToast("Produto salvo.", "success");
    await renderProductsAdmin();
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

  function formToObject(data, strings, numbers) {
    const output = {};
    strings.forEach((key) => output[key] = String(data.get(key) || "").trim());
    numbers.forEach((key) => output[key] = Number(data.get(key) || 0));
    return output;
  }

  function can(permission) {
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
    return Number(value || 0).toLocaleString("pt-BR", { maximumFractionDigits: 2 });
  }

  function formatMoney(value) {
    return Number(value || 0).toLocaleString("pt-BR", { minimumFractionDigits: 2, maximumFractionDigits: 2 });
  }

  function formatDate(value) {
    return value ? new Date(value).toLocaleDateString("pt-BR") : "";
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

  function refreshIcons() {}

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
