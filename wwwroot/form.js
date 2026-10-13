(() => {
  const params = new URLSearchParams(window.location.search);
  const state = {
    roomKey: params.get("room") || "",
    status: params.get("status") || "",
    auth: null,
    screenVisibility: defaultScreenVisibility(),
    visibilitySyncTimer: null,
    roomTimerInterval: null
  };

  const fieldAliases = {
    guestName: "guestName",
    reservationCode: "guestCode",
    cpf: "cpf",
    phone: "phone",
    emergencyContact: "emergencyContact",
    emergencyPhone: "emergencyPhone",
    bloodType: "bloodType",
    checkIn: "checkIn",
    checkOut: "checkOut",
    actualCheckOut: "actualCheckOut",
    note: "note",
    operator: "operator"
  };

  const elements = {
    content: document.getElementById("formContent"),
    message: document.getElementById("formMessage"),
    title: document.getElementById("formTitle"),
    subtitle: document.getElementById("formSubtitle")
  };

  document.addEventListener("DOMContentLoaded", () => {
    init().catch((error) => {
      showMessage(error.message || "Não foi possível abrir o formulário.", "error");
    });
  });

  async function init() {
    try {
      await loadRuntimeContext();
      await render();
      startVisibilitySync();
      startRoomTimer();
      refreshIcons();
    } catch (error) {
      showMessage(error.message || "Não foi possível abrir o formulário.", "error");
    }
  }

  async function loadRuntimeContext() {
    const [auth, screenVisibility] = await Promise.all([
      fetchJson("/api/auth/me").catch(() => null),
      fetchJson("/api/access/screen-visibility/current").catch(() => defaultScreenVisibility())
    ]);
    state.auth = auth;
    state.screenVisibility = normalizeScreenVisibility(screenVisibility);
  }

  function startVisibilitySync() {
    if (state.visibilitySyncTimer) {
      window.clearInterval(state.visibilitySyncTimer);
    }

    state.visibilitySyncTimer = window.setInterval(async () => {
      const before = visibilitySignature();
      await loadRuntimeContext();
      if (before !== visibilitySignature()) {
        await render();
      }
    }, 10000);
  }

  function startRoomTimer() {
    if (state.roomTimerInterval) {
      window.clearInterval(state.roomTimerInterval);
    }

    state.roomTimerInterval = window.setInterval(() => {
      document.querySelectorAll("[data-occupancy-timer]").forEach((element) => {
        element.textContent = formatElapsed(element.dataset.occupancyTimer);
      });
    }, 1000);
  }

  function visibilitySignature() {
    return JSON.stringify({
      role: state.auth?.user?.role || "",
      hiddenFieldsByRole: state.screenVisibility?.hiddenFieldsByRole || {}
    });
  }

  async function render() {
    const data = await loadRoomReservations();
    const room = findRoom(data, state.roomKey);
    if (!room) {
      elements.content.innerHTML = `
        <section class="form-card">
          <div class="form-card-header">
            <div>
              <span class="tag bad">Erro</span>
              <h2>Cama não encontrada</h2>
            </div>
          </div>
          <p>Volte para Reservas e escolha uma cama novamente.</p>
          <a class="button primary" href="/?view=rooms"><i data-lucide="arrow-left"></i><span>Voltar</span></a>
        </section>
      `;
      return;
    }

    const selectedStatus = roomStatuses().find((item) => item.id === state.status) || null;
    elements.title.textContent = room.label;
    elements.subtitle.textContent = selectedStatus
      ? "Preencha os campos e salve para voltar à tela de Reservas."
      : "Escolha o novo status para continuar.";

    elements.content.innerHTML = selectedStatus
      ? renderDetailsForm(room, selectedStatus)
      : renderStatusPicker(room);

    bindActions();
    refreshIcons();
  }

  function renderStatusPicker(room) {
    const current = roomStatuses().find((item) => item.id === room.status) || roomStatuses()[0];
    return `
      <section class="form-card ${escapeAttribute(room.status)}">
        <div class="form-card-header">
          <div>
            <span class="tag ${escapeAttribute(current.tone)}">${escapeHtml(current.label)}</span>
            <h2>${escapeHtml(room.label)}</h2>
          </div>
          <a class="button ghost" href="/?view=rooms"><i data-lucide="x"></i><span>Cancelar</span></a>
        </div>
        <div class="status-picker">
          ${roomStatuses().map((status) => `
            <button class="status-choice ${escapeAttribute(status.id)} ${room.status === status.id ? "selected" : ""}" type="button" data-status="${escapeAttribute(status.id)}">
              <i data-lucide="${escapeAttribute(roomStatusIcon(status.id))}"></i>
              <span>${escapeHtml(status.label)}</span>
            </button>
          `).join("")}
        </div>
      </section>
    `;
  }

  function renderDetailsForm(room, status) {
    const showGuestName = !isBedFieldHidden(fieldAliases.guestName);
    const showReservationCode = !isBedFieldHidden(fieldAliases.reservationCode);
    const showCpf = !isBedFieldHidden(fieldAliases.cpf);
    const showPhone = !isBedFieldHidden(fieldAliases.phone);
    const showEmergencyContact = !isBedFieldHidden(fieldAliases.emergencyContact);
    const showEmergencyPhone = !isBedFieldHidden(fieldAliases.emergencyPhone);
    const showBloodType = !isBedFieldHidden(fieldAliases.bloodType);
    const showCheckIn = !isBedFieldHidden(fieldAliases.checkIn);
    const showCheckOut = !isBedFieldHidden(fieldAliases.checkOut);
    const showActualCheckOut = !isBedFieldHidden(fieldAliases.actualCheckOut);
    const showOperator = !isBedFieldHidden(fieldAliases.operator);
    const showNote = !isBedFieldHidden(fieldAliases.note);
    const showTimer = ["reserved", "occupied"].includes(status.id) && room.occupancyStartedAt;

    return `
      <section class="form-card ${escapeAttribute(status.id)}">
        <div class="form-card-header">
          <div>
            <span class="tag ${escapeAttribute(status.tone)}">${escapeHtml(status.label)}</span>
            <h2>Informações da ${escapeHtml(room.label)}</h2>
          </div>
          <a class="button ghost" href="/form?room=${encodeURIComponent(state.roomKey)}"><i data-lucide="arrow-left"></i><span>Status</span></a>
        </div>
        ${showTimer ? `
          <div class="form-timer">
            <i data-lucide="timer"></i>
            <span>Tempo em uso</span>
            <strong data-occupancy-timer="${escapeAttribute(room.occupancyStartedAt)}">${escapeHtml(formatElapsed(room.occupancyStartedAt))}</strong>
          </div>
        ` : ""}
        <form class="room-form" data-room-form>
          <label>
            <span>Identificação da cama</span>
            <input name="roomLabel" value="${escapeAttribute(room.label || "")}" required>
          </label>
          <label>
            <span>Tipo/categoria</span>
            <input name="category" value="${escapeAttribute(room.category || "Leito")}">
          </label>
          <label>
            <span>Capacidade</span>
            <input name="capacity" type="number" min="1" value="${escapeAttribute(room.capacity || 1)}" required>
          </label>
          ${showReservationCode ? `
          <label>
            <span>Código interno do hóspede</span>
            <input name="reservationCode" value="${escapeAttribute(room.reservationCode || "")}">
          </label>
          ` : ""}
          ${showGuestName ? `
          <label>
            <span>Nome do hóspede</span>
            <input name="guestName" value="${escapeAttribute(room.guestName || "")}">
          </label>
          ` : ""}
          ${showCpf ? `
          <label>
            <span>CPF</span>
            <input name="cpf" inputmode="numeric" value="${escapeAttribute(room.cpf || "")}">
          </label>
          ` : ""}
          ${showPhone ? `
          <label>
            <span>Telefone</span>
            <input name="phone" inputmode="tel" value="${escapeAttribute(room.phone || "")}">
          </label>
          ` : ""}
          ${showEmergencyContact ? `
          <label>
            <span>Contato de emergência</span>
            <input name="emergencyContact" value="${escapeAttribute(room.emergencyContact || "")}">
          </label>
          ` : ""}
          ${showEmergencyPhone ? `
          <label>
            <span>Telefone de emergência</span>
            <input name="emergencyPhone" inputmode="tel" value="${escapeAttribute(room.emergencyPhone || "")}">
          </label>
          ` : ""}
          ${showBloodType ? `
          <label>
            <span>Tipo sanguíneo</span>
            <input name="bloodType" value="${escapeAttribute(room.bloodType || "")}">
          </label>
          ` : ""}
          ${showCheckIn ? `
          <label>
            <span>Entrada prevista</span>
            <input name="checkIn" type="date" value="${escapeAttribute(room.checkIn || "")}">
          </label>
          ` : ""}
          ${showCheckOut ? `
          <label>
            <span>Saída prevista</span>
            <input name="checkOut" type="date" value="${escapeAttribute(room.checkOut || "")}">
          </label>
          ` : ""}
          ${showActualCheckOut ? `
          <label>
            <span>Saída real</span>
            <input name="actualCheckOut" type="date" value="${escapeAttribute(room.actualCheckOut || "")}">
          </label>
          ` : ""}
          ${showOperator ? `
          <label class="full">
            <span>Responsável pela alteração</span>
            <input name="operator" value="${escapeAttribute(room.operator || "")}" placeholder="Ex.: João da recepção">
          </label>
          ` : ""}
          ${showNote ? `
          <label class="full">
            <span>Observação operacional</span>
            <input name="note" value="${escapeAttribute(room.note || "")}" placeholder="Ex.: vistoria, limpeza, manutenção">
          </label>
          ` : ""}
          <div class="form-actions">
            <a class="button ghost" href="/?view=rooms">Cancelar</a>
            <button class="button primary" type="submit"><i data-lucide="save"></i><span>Salvar e voltar</span></button>
          </div>
        </form>
      </section>
    `;
  }

  function bindActions() {
    elements.content.querySelectorAll("[data-status]").forEach((button) => {
      button.addEventListener("click", () => {
        state.status = button.dataset.status || "available";
        const url = new URL(window.location.href);
        url.searchParams.set("status", state.status);
        window.history.replaceState({}, "", url);
        render().catch((error) => showMessage(error.message || "Não foi possível abrir o formulário.", "error"));
      });
    });

    elements.content.querySelector("[data-room-form]")?.addEventListener("submit", async (event) => {
      event.preventDefault();
      try {
        await saveForm(event.currentTarget);
        window.location.href = "/?view=rooms";
      } catch (error) {
        showMessage(error.message || "Não foi possível salvar.", "error");
      }
    });
  }

  async function saveForm(form) {
    const formData = new FormData(form);
    const data = await loadRoomReservations();
    const room = findRoom(data, state.roomKey);
    if (!room) throw new Error("Cama não encontrada.");
    const preserveFields = new Set(
      Object.values(fieldAliases).filter((fieldId) => isBedFieldHidden(fieldId))
    );
    const operator = preserveFields.has("operator")
      ? room.operator || ""
      : sanitizeOperationalNote(String(formData.get("operator") || ""));
    const note = preserveFields.has("note")
      ? room.note || ""
      : String(formData.get("note") || "");

    await updateRoomReservation(
      state.roomKey,
      state.status || "available",
      preserveFields.has("guestName") ? room.guestName || "" : String(formData.get("guestName") || ""),
      preserveFields.has("guestCode") ? room.reservationCode || "" : String(formData.get("reservationCode") || ""),
      preserveFields.has("cpf") ? room.cpf || "" : String(formData.get("cpf") || ""),
      preserveFields.has("phone") ? room.phone || "" : String(formData.get("phone") || ""),
      preserveFields.has("emergencyContact") ? room.emergencyContact || "" : String(formData.get("emergencyContact") || ""),
      preserveFields.has("emergencyPhone") ? room.emergencyPhone || "" : String(formData.get("emergencyPhone") || ""),
      preserveFields.has("bloodType") ? room.bloodType || "" : String(formData.get("bloodType") || ""),
      preserveFields.has("checkIn") ? room.checkIn || "" : String(formData.get("checkIn") || ""),
      preserveFields.has("checkOut") ? room.checkOut || "" : String(formData.get("checkOut") || ""),
      preserveFields.has("actualCheckOut") ? room.actualCheckOut || "" : String(formData.get("actualCheckOut") || ""),
      note,
      String(formData.get("roomLabel") || ""),
      String(formData.get("category") || ""),
      Number(formData.get("capacity") || 1),
      operator,
      preserveFields
    );
  }

  async function loadRoomReservations() {
    return normalizeRoomReservations(await fetchJson("/api/rooms"));
  }

  function defaultRoomReservations() {
    return {
      modelVersion: 3,
      areas: [
        {
          id: "camas",
          name: "Camas",
          rooms: Array.from({ length: 24 }, (_, index) => {
            const number = index + 1;
            if ([2, 7, 13, 19].includes(number)) return roomSeed(`Cama ${number}`, "Leito", 1, "reserved", `RSV-${2400 + number}`, 1, 3);
            if ([3, 9, 16, 22].includes(number)) return roomSeed(`Cama ${number}`, "Leito", 1, "occupied", `RSV-${2380 + number}`, -2, 1);
            if ([5, 18].includes(number)) return roomSeed(`Cama ${number}`, "Leito", 1, "cleaning", "", "", "", "Limpeza programada");
            if ([12, 24].includes(number)) return roomSeed(`Cama ${number}`, "Leito", 1, "maintenance", "", "", "", "Manutenção preventiva");
            return roomSeed(`Cama ${number}`, "Leito", 1, "available");
          })
        }
      ],
      history: []
    };
  }

  function normalizeRoomReservations(data) {
    const fallback = defaultRoomReservations();
    const isCurrentModel = [3, 4].includes(Number(data?.modelVersion)) && Array.isArray(data.areas) && data.areas[0]?.id === "camas";
    if (!isCurrentModel) {
      markBaseBeds(fallback);
      return fallback;
    }

    data.history = Array.isArray(data.history) ? data.history : [];
    data.areas[0].name = "Camas";
    data.areas[0].rooms = Array.isArray(data.areas[0].rooms) ? data.areas[0].rooms : fallback.areas[0].rooms;
    markBaseBeds(data);
    return data;
  }

  function findRoom(data, roomKey) {
    const [areaId, roomId] = String(roomKey || "").split("|");
    const area = data.areas.find((item) => item.id === areaId);
    const room = area?.rooms.find((item) => item.id === roomId);
    return room && area ? { ...room, areaId: area.id, areaName: area.name } : null;
  }

  async function updateRoomReservation(
    roomKey,
    status,
    guestName = "",
    reservationCode = "",
    cpf = "",
    phone = "",
    emergencyContact = "",
    emergencyPhone = "",
    bloodType = "",
    checkIn = "",
    checkOut = "",
    actualCheckOut = "",
    note = "",
    roomLabel = "",
    category = "",
    capacity = 1,
    operator = "") {
    const [areaId, roomId] = String(roomKey || "").split("|");
    if (areaId !== "camas" || !roomId) throw new Error("Cama não encontrada.");

    await fetchJson(`/api/rooms/${encodeURIComponent(roomId)}`, {
      method: "PUT",
      body: JSON.stringify({
        roomLabel,
        category,
        capacity,
        status,
        guestName,
        reservationCode,
        cpf,
        phone,
        emergencyContact,
        emergencyPhone,
        bloodType,
        checkIn,
        checkOut,
        actualCheckOut,
        note,
        operator
      })
    });
  }

  function roomSeed(label, category, capacity, status, reservationCode = "", checkInOffset = "", checkOutOffset = "", note = "") {
    const checkIn = checkInOffset === "" ? "" : toDateInputValue(addDays(new Date(), Number(checkInOffset)));
    const checkOut = checkOutOffset === "" ? "" : toDateInputValue(addDays(new Date(), Number(checkOutOffset)));
    return {
      id: slugify(label),
      label,
      category,
      capacity,
      status,
      reservationCode,
      checkIn,
      checkOut,
      note,
      updatedAt: new Date().toISOString()
    };
  }

  function markBaseBeds(data) {
    const rooms = data.areas?.[0]?.rooms || [];
    rooms.forEach((room) => {
      const match = /^Cama\s+(\d+)$/i.exec(room.label || "");
      const number = match ? Number(match[1]) : 0;
      room.fixed = number >= 1 && number <= 24 ? true : room.fixed === true;
    });
  }

  function formatElapsed(value) {
    const startedAt = Date.parse(value || "");
    if (!Number.isFinite(startedAt)) return "00:00:00";
    const totalSeconds = Math.max(0, Math.floor((Date.now() - startedAt) / 1000));
    const days = Math.floor(totalSeconds / 86400);
    const hours = Math.floor((totalSeconds % 86400) / 3600);
    const minutes = Math.floor((totalSeconds % 3600) / 60);
    const seconds = totalSeconds % 60;
    const clock = [hours, minutes, seconds].map((part) => String(part).padStart(2, "0")).join(":");
    return days ? `${days}d ${clock}` : clock;
  }

  function roomStatuses() {
    return [
      { id: "available", label: "Disponível", tone: "good" },
      { id: "reserved", label: "Reservado", tone: "warn" },
      { id: "occupied", label: "Ocupado", tone: "bad" },
      { id: "cleaning", label: "Limpeza", tone: "warn" },
      { id: "maintenance", label: "Manutenção", tone: "" }
    ];
  }

  function roomStatusIcon(status) {
    return {
      available: "check",
      reserved: "calendar-plus",
      occupied: "door-closed",
      cleaning: "sparkles",
      maintenance: "wrench"
    }[status] || "circle";
  }

  function defaultScreenVisibility() {
    return {
      hiddenScreensByRole: { admin: [], standard: [] },
      hiddenFieldsByRole: { admin: [], standard: [] }
    };
  }

  function normalizeScreenVisibility(settings = {}) {
    const fallback = defaultScreenVisibility();
    return {
      hiddenScreensByRole: {
        ...fallback.hiddenScreensByRole,
        ...(settings.hiddenScreensByRole || {})
      },
      hiddenFieldsByRole: {
        ...fallback.hiddenFieldsByRole,
        ...(settings.hiddenFieldsByRole || {})
      }
    };
  }

  function isDeveloperUser() {
    return state.auth?.isAuthenticated && state.auth?.user?.role === "developer";
  }

  function isBedFieldHidden(fieldId) {
    if (isDeveloperUser()) {
      return false;
    }

    const role = state.auth?.user?.role || "";
    const hidden = state.screenVisibility?.hiddenFieldsByRole?.[role] || [];
    return hidden.includes(fieldId);
  }

  async function fetchJson(url, options = {}) {
    const response = await fetch(url, {
      ...options,
      headers: {
        Accept: "application/json",
        ...(options.body instanceof FormData ? {} : { "Content-Type": "application/json" }),
        ...(options.headers || {})
      }
    });
    if (!response.ok) {
      let message = "Falha ao carregar dados do servidor.";
      try {
        message = (await response.json()).message || message;
      } catch {
        // Mantem a mensagem generica quando a resposta nao for JSON.
      }
      throw new Error(message);
    }

    return response.status === 204 ? null : response.json();
  }

  function readStoredJson(key, fallback) {
    try {
      return JSON.parse(localStorage.getItem(key)) || fallback;
    } catch {
      return fallback;
    }
  }

  function slugify(value) {
    return String(value || "")
      .normalize("NFD")
      .replace(/[\u0300-\u036f]/g, "")
      .toLowerCase()
      .replace(/[^a-z0-9]+/g, "-")
      .replace(/^-+|-+$/g, "")
      || `cama-${Date.now()}`;
  }

  function sanitizeBedLabel(value) {
    return String(value || "").replace(/\s+/g, " ").trim().slice(0, 48);
  }

  function sanitizeReservationCode(value) {
    return String(value || "").replace(/[^a-zA-Z0-9._-]/g, "").slice(0, 24).toUpperCase();
  }

  function sanitizeOperationalNote(value) {
    return String(value || "").replace(/[<>]/g, "").replace(/\s+/g, " ").trim().slice(0, 160);
  }

  function addDays(date, days) {
    const next = new Date(date);
    next.setDate(next.getDate() + days);
    return next;
  }

  function todayInputValue() {
    return toDateInputValue(new Date());
  }

  function toDateInputValue(date) {
    const offsetDate = new Date(date.getTime() - date.getTimezoneOffset() * 60000);
    return offsetDate.toISOString().slice(0, 10);
  }

  function escapeHtml(value) {
    return String(value ?? "")
      .replace(/&/g, "&amp;")
      .replace(/</g, "&lt;")
      .replace(/>/g, "&gt;")
      .replace(/"/g, "&quot;")
      .replace(/'/g, "&#39;");
  }

  function escapeAttribute(value) {
    return escapeHtml(value).replace(/`/g, "&#96;");
  }

  function showMessage(message, type = "") {
    elements.message.textContent = message;
    elements.message.className = `message ${type}`.trim();
    elements.message.classList.remove("hidden");
  }

  function refreshIcons() {
    if (window.lucide) {
      window.lucide.createIcons();
    }
  }
})();
