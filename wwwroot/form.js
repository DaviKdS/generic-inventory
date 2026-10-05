(() => {
  const STORAGE_KEY = "generic-inventory.roomReservations";
  const params = new URLSearchParams(window.location.search);
  const state = {
    roomKey: params.get("room") || "",
    status: params.get("status") || ""
  };

  const elements = {
    content: document.getElementById("formContent"),
    message: document.getElementById("formMessage"),
    title: document.getElementById("formTitle"),
    subtitle: document.getElementById("formSubtitle")
  };

  document.addEventListener("DOMContentLoaded", init);

  function init() {
    try {
      render();
      refreshIcons();
    } catch (error) {
      showMessage(error.message || "Não foi possível abrir o formulário.", "error");
    }
  }

  function render() {
    const data = loadRoomReservations();
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
    return `
      <section class="form-card ${escapeAttribute(status.id)}">
        <div class="form-card-header">
          <div>
            <span class="tag ${escapeAttribute(status.tone)}">${escapeHtml(status.label)}</span>
            <h2>Informações da ${escapeHtml(room.label)}</h2>
          </div>
          <a class="button ghost" href="/form?room=${encodeURIComponent(state.roomKey)}"><i data-lucide="arrow-left"></i><span>Status</span></a>
        </div>
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
          <label>
            <span>Código da reserva</span>
            <input name="reservationCode" value="${escapeAttribute(room.reservationCode || "")}" ${["reserved", "occupied"].includes(status.id) ? "" : "disabled"}>
          </label>
          <label>
            <span>Entrada prevista</span>
            <input name="checkIn" type="date" value="${escapeAttribute(room.checkIn || "")}" ${["reserved", "occupied"].includes(status.id) ? "" : "disabled"}>
          </label>
          <label>
            <span>Saída prevista</span>
            <input name="checkOut" type="date" value="${escapeAttribute(room.checkOut || "")}" ${["reserved", "occupied"].includes(status.id) ? "" : "disabled"}>
          </label>
          <label class="full">
            <span>Responsável pela alteração</span>
            <input name="operator" value="${escapeAttribute(room.operator || "")}" placeholder="Ex.: João da recepção">
          </label>
          <label class="full">
            <span>Observação operacional</span>
            <input name="note" value="${escapeAttribute(room.note || "")}" placeholder="Ex.: vistoria, limpeza, manutenção">
          </label>
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
        render();
      });
    });

    elements.content.querySelector("[data-room-form]")?.addEventListener("submit", (event) => {
      event.preventDefault();
      try {
        saveForm(event.currentTarget);
        window.location.href = "/?view=rooms";
      } catch (error) {
        showMessage(error.message || "Não foi possível salvar.", "error");
      }
    });
  }

  function saveForm(form) {
    const formData = new FormData(form);
    const operator = sanitizeOperationalNote(String(formData.get("operator") || ""));
    const note = String(formData.get("note") || "");

    updateRoomReservation(
      state.roomKey,
      state.status || "available",
      String(formData.get("reservationCode") || ""),
      String(formData.get("checkIn") || ""),
      String(formData.get("checkOut") || ""),
      note,
      String(formData.get("roomLabel") || ""),
      String(formData.get("category") || ""),
      Number(formData.get("capacity") || 1),
      operator
    );
  }

  function loadRoomReservations() {
    return normalizeRoomReservations(readStoredJson(STORAGE_KEY, defaultRoomReservations()));
  }

  function saveRoomReservations(data) {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(data));
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
    const isCurrentModel = data?.modelVersion === 3 && Array.isArray(data.areas) && data.areas[0]?.id === "camas";
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

  function updateRoomReservation(roomKey, status, reservationCode = "", checkIn = "", checkOut = "", note = "", roomLabel = "", category = "", capacity = 1, operator = "") {
    const data = loadRoomReservations();
    const [areaId, roomId] = String(roomKey || "").split("|");
    const area = data.areas.find((item) => item.id === areaId);
    const room = area?.rooms.find((item) => item.id === roomId);
    if (!area || !room) throw new Error("Cama não encontrada.");

    const nextLabel = sanitizeBedLabel(roomLabel || room.label);
    if (!nextLabel) throw new Error("Informe a identificação da cama.");

    const nextId = slugify(nextLabel);
    if (area.rooms.some((item) => item !== room && (item.id === nextId || item.label.toLowerCase() === nextLabel.toLowerCase()))) {
      throw new Error("Já existe uma cama com esta identificação.");
    }

    const safeCode = sanitizeReservationCode(reservationCode || room.reservationCode || "");
    const nextCode = ["reserved", "occupied"].includes(status)
      ? safeCode || `RSV-${String(Date.now()).slice(-6)}`
      : "";

    room.id = nextId;
    room.label = nextLabel;
    room.category = sanitizeOperationalNote(category) || "Leito";
    room.capacity = Math.max(1, Number(capacity || 1));
    room.status = status;
    room.reservationCode = nextCode;
    room.checkIn = ["reserved", "occupied"].includes(status) ? checkIn || room.checkIn || todayInputValue() : "";
    room.checkOut = ["reserved", "occupied"].includes(status) ? checkOut || room.checkOut || "" : "";
    room.note = sanitizeOperationalNote(note || (status === "cleaning" ? "Limpeza em andamento" : status === "maintenance" ? "Manutenção programada" : ""));
    room.operator = sanitizeOperationalNote(operator || room.operator || "");
    room.updatedAt = new Date().toISOString();

    data.history.unshift({
      date: room.updatedAt,
      areaName: area.name,
      roomLabel: room.label,
      status,
      reservationCode: room.reservationCode,
      note: room.operator ? `${room.note} - Responsável: ${room.operator}` : room.note
    });
    data.history = data.history.slice(0, 60);
    saveRoomReservations(data);
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
