// ============================================================================
// Cau hinh chung — goi thang cac action co san trong HikvisionController
// (controller MVC mac dinh, route dang /Hikvision/{Action})
// ============================================================================
const API_BASE = "/Hikvision";
const PAGE_SIZE = 12;

const state = {
  keyword: "",
  page: 0,          // Hikvision search dung page bat dau tu 0
  total: 0,          // tong so ban ghi khop (neu thiet bi tra ve numOfMatches), khong thi = so ban ghi trang hien tai
  lastPageItemCount: 0,
  editingEmployeeNo: null,   // null => dang them moi
  deletingEmployeeNo: null,
  attendanceEmployeeNo: null,
  pendingFaceFile: null,     // File duoc chon o modal them/sua, upload sau khi luu thong tin
  capturedFingerNo: null,    // ngon dang cho luu sau khi da chup (null = chua chup ngon nao)
  capturedFingerData: null   // fingerData (base64 tho) sau khi goi CaptureFingerPrint, cho toi khi bam "Luu"
};

// ============================================================================
// Icon helpers (inline SVG, dung chung mau currentColor)
// ============================================================================
const ICONS = {
  face: `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round"><rect x="4" y="4" width="16" height="16" rx="4"/><circle cx="9" cy="10" r="1"/><circle cx="15" cy="10" r="1"/><path d="M9 15c1 1 5 1 6 0"/></svg>`,
  fingerprint: `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round"><path d="M12 2a5 5 0 0 0-5 5v2a5 5 0 0 0 10 0V7a5 5 0 0 0-5-5Z"/><path d="M6 12v1a6 6 0 0 0 12 0v-1M9 21c-1-2-1.5-4-1.5-6M15 21c1-2 1.5-4 1.5-6"/></svg>`,
  card: `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round"><rect x="2" y="5" width="20" height="14" rx="2"/><path d="M2 10h20M6 15h4"/></svg>`,
  edit: `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round"><path d="M12 20h9M16.5 3.5a2.1 2.1 0 0 1 3 3L7 19l-4 1 1-4Z"/></svg>`,
  history: `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round"><path d="M3 12a9 9 0 1 0 3-6.7"/><path d="M3 4v5h5M12 7v5l4 2"/></svg>`,
  trash: `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round"><path d="M3 6h18M8 6V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2m3 0-1 14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2L4 6"/></svg>`,
  fingerprint2: `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" style="color: var(--surface);"><path d="M12 2a5 5 0 0 0-5 5v2a5 5 0 0 0 10 0V7a5 5 0 0 0-5-5Z"/><path d="M6 12v1a6 6 0 0 0 12 0v-1M9 21c-1-2-1.5-4-1.5-6M15 21c1-2 1.5-4 1.5-6"/></svg>`,
  trash2: `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" style="color: var(--surface);"><path d="M3 6h18M8 6V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2m3 0-1 14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2L4 6"/></svg>`
};

// ============================================================================
// Utils
// ============================================================================
function debounce(fn, delay) {
  let t;
  return (...args) => { clearTimeout(t); t = setTimeout(() => fn(...args), delay); };
}

function toDateInputValue(dateStr) {
  const d = new Date(dateStr);
  if (isNaN(d)) return "";
  return d.toISOString().slice(0, 10);
}

function formatDateVN(dateStr) {
  const d = new Date(dateStr);
  if (isNaN(d)) return "-";
  return d.toLocaleDateString("vi-VN");
}

function formatDateTimeVN(dateStr) {
  const d = new Date(dateStr);
  if (isNaN(d)) return "-";
  return d.toLocaleString("vi-VN", { day: "2-digit", month: "2-digit", year: "numeric", hour: "2-digit", minute: "2-digit", second: "2-digit" });
}

// yyyy-MM-ddTHH:mm:ss theo dung format ma AcsEventSearch/PersonInfo dang doi (server ToString("yyyy-MM-ddTHH:mm:ss"))
function toIsapiDateTime(dateInputValue, endOfDay) {
  const base = dateInputValue ? new Date(dateInputValue) : new Date();
  if (endOfDay) base.setHours(23, 59, 59, 0);
  else base.setHours(0, 0, 0, 0);
  const pad = n => String(n).padStart(2, "0");
  return `${base.getFullYear()}-${pad(base.getMonth() + 1)}-${pad(base.getDate())}T${pad(base.getHours())}:${pad(base.getMinutes())}:${pad(base.getSeconds())}`;
}

function escapeHtml(str) {
  return String(str ?? "").replace(/[&<>"']/g, m => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[m]));
}

// Trang web chay HTTPS (VD https://localhost:7193) se bi trinh duyet chan "mixed content"
// neu <img src> tro thang toi http://{ip-thiet-bi}/..., nen phai doi anh qua ProxyImage cua backend.
function proxyImageUrl(deviceUrl) {
  if (!deviceUrl) return "";
  return `${API_BASE}/ProxyImage?url=${encodeURIComponent(deviceUrl)}`;
}

function showToast(message, type = "success") {
  const stack = document.getElementById("toastStack");
  const el = document.createElement("div");
  el.className = `toast ${type}`;
  el.textContent = message;
  stack.appendChild(el);
  setTimeout(() => el.remove(), 3200);
}

function openModal(id) { document.getElementById(id).classList.add("show"); }
function closeModal(id) { document.getElementById(id).classList.remove("show"); }

document.querySelectorAll("[data-close]").forEach(btn => {
  btn.addEventListener("click", () => {
    closeModal(btn.dataset.close);
    // Dong modal them/sua: reload lai the nhan vien phia sau de cap nhat so luong
    // khuon mat/van tay neu nguoi dung da xoa/them ma khong bam "Luu nhan vien".
    if (btn.dataset.close === "employeeModalOverlay") loadEmployees();
  });
});
document.querySelectorAll(".overlay").forEach(overlay => {
  overlay.addEventListener("click", e => { if (e.target === overlay) overlay.classList.remove("show"); });
});

// Kiem tra ma trang thai tra ve tu thiet bi Hikvision (HikBaseResponse / FaceUploadResponse: statusCode 1 = OK)
function assertHikOk(obj, fallbackMsg) {
  if (obj && typeof obj.statusCode !== "undefined" && obj.statusCode !== 1) {
    throw new Error(obj.statusString || fallbackMsg || "Thiết bị từ chối yêu cầu.");
  }
}

// ============================================================================
// API calls — goi thang cac action trong HikvisionController
// ============================================================================
async function apiListUser(keyword, page, pageSize) {
  const res = await fetch(`${API_BASE}/ListUser?page=${page}&pageSize=${pageSize}`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    // Controller: keyword = EmployeeNo neu co, khong thi lay Name — gui chung 1 tu khoa vao EmployeeNo
    body: JSON.stringify({ employeeNo: keyword, name: "" })
  });
  if (!res.ok) throw new Error("Không tải được danh sách nhân viên từ thiết bị.");
  return res.json(); // UserSearchResult { items, total }
}

async function apiGetUser(employeeNo) {
  const res = await fetch(`${API_BASE}/GetUser`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ employeeNo })
  });
  if (!res.ok) throw new Error("Không lấy được thông tin nhân viên từ thiết bị.");
  const list = await res.json(); // List<PersonInfo>
  return Array.isArray(list) && list.length ? list[0] : null;
}

async function apiCreateUser(personInfo) {
  const res = await fetch(`${API_BASE}/CreateUserInfo`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(personInfo)
  });
  const raw = await res.json(); // chuoi JSON tho tu thiet bi (ISAPI tra ve string)
  if (!res.ok) throw new Error("Không thể tạo nhân viên trên thiết bị.");
  try {
    const device = JSON.parse(raw);
    assertHikOk(device, "Thiết bị từ chối tạo nhân viên.");
  } catch { /* body khong phai JSON chuan, bo qua kiem tra chi tiet */ }
  return raw;
}

async function apiUpdateUser(personInfo) {
  // Dung chung action ActiveUser và UpdateUser (UpdateUserAsync = Modify toan bo UserInfo)
  const res = await fetch(`${API_BASE}/UpdateUser`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(personInfo)
  });
  if (!res.ok) throw new Error("Không thể cập nhật nhân viên trên thiết bị.");
  const data = await res.json(); // HikBaseResponse
  assertHikOk(data, "Thiết bị từ chối cập nhật nhân viên.");
  return data;
}

async function apiDeleteUser(employeeNo) {
  const res = await fetch(`${API_BASE}/DeleteUser`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ employeeNo })
  });
  if (!res.ok) throw new Error("Không thể xóa nhân viên trên thiết bị.");
  const data = await res.json(); // HikBaseResponse
  assertHikOk(data, "Thiết bị từ chối xóa nhân viên.");
  return data;
}

async function apiUploadFace(employeeNo, file) {
  const fd = new FormData();
  fd.append("file", file);
  fd.append("employeeNo", employeeNo);
  const res = await fetch(`${API_BASE}/UploadFace`, { method: "POST", body: fd });
  if (!res.ok) throw new Error("Không thể tải ảnh khuôn mặt lên thiết bị.");
  const data = await res.json(); // FaceUploadResponse
  assertHikOk(data, "Thiết bị từ chối ảnh khuôn mặt (kiểm tra lại chất lượng/kích thước ảnh).");
  return data;
}

async function apiDeleteFace(employeeNo) {
  const res = await fetch(`${API_BASE}/DeleteFace`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ employeeNo })
  });
  if (!res.ok) throw new Error("Không thể xóa khuôn mặt trên thiết bị.");
  const data = await res.json(); // HikBaseResponse
  assertHikOk(data, "Thiết bị từ chối xóa khuôn mặt.");
  return data;
}

async function apiCaptureFingerPrint(fingerNo) {
  const res = await fetch(`${API_BASE}/CaptureFingerPrint?fingerNo=${fingerNo}`, { method: "POST" });
  if (!res.ok) throw new Error("Không chụp được vân tay — kiểm tra đã đặt tay lên đầu đọc trên thiết bị chưa.");
  return res.json(); // FingerPrintCaptureResult { fingerData, fingerNo, fingerPrintQuality }
}

async function apiSetUpFingerPrint(employeeNo, fingerData, fingerNo) {
  const res = await fetch(`${API_BASE}/SetUpFingerPrint`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ employeeNo, fingerData, fingerNo })
  });
  if (!res.ok) throw new Error("Không thể đăng ký vân tay lên thiết bị.");
  return res.json(); // FingerPrintSetUpResponse
}

async function apiDeleteFingerPrint(employeeNo, fingerNo) {
  const res = await fetch(`${API_BASE}/DeleteFingerPrint`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ employeeNo, fingerNo })
  });
  if (!res.ok) throw new Error("Không thể xóa vân tay trên thiết bị.");
  const data = await res.json(); // HikBaseResponse
  
  if(data.fingerPrintStatus.status !== "success"){
    throw new Error("Thiết bị từ chối xóa vân tay.");
  }
  
  return data;
}

async function apiGetFingerPrints(employeeNo) {
  const res = await fetch(`${API_BASE}/GetFingerPrints`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ employeeNo })
  });
  if (!res.ok) throw new Error("Không thể lấy danh sách vân tay trên thiết bị.");
  return res.json(); // Array of FingerPrintInfoModel
}

async function apiSearchAttendance(employeeNo, startTime, endTime) {
  const res = await fetch(`${API_BASE}/SearchAcsEventUser`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ employeeNo, startTime, endTime })
  });
  if (!res.ok) throw new Error("Không tải được lịch sử checkin/out.");
  return res.json(); // List<PersonEventInfo>
}

// ============================================================================
// Render: employee card grid
// ============================================================================
function bioChip(iconKey, count, label) {
  const zero = count === 0 ? "zero" : "";
  return `<span class="bio-chip ${zero}" title="${label}: ${count}">${ICONS[iconKey]}<b style="color: var(--primary)">${count}</b></span>`;
}

function renderEmployeeCard(emp) {
  const disabledClass = emp.userType === "normal" ? "" : "disabled";
  const typeClass = `type-${emp.userType}`;
  const avatar = emp.faceUrl && emp.faceUrl.trim() !== ""
    ? proxyImageUrl(emp.faceUrl)
    : `https://api.dicebear.com/7.x/initials/svg?seed=${encodeURIComponent(emp.name)}`;

  return `
    <div class="employee-card ${disabledClass}" data-emp-no="${escapeHtml(emp.employeeNo)}">

      <div class="card-top">
        <span class="type-badge ${typeClass}">${escapeHtml(emp.userType)}</span>
        <label class="switch" title="${emp.enabled ? "Đang áp dụng thời hạn hiệu lực" : "Hiệu lực vĩnh viễn"}">
          <input type="checkbox" class="toggle-active" ${emp.userType === "normal" ? "checked" : ""} data-emp-no="${escapeHtml(emp.employeeNo)}" />
          <span class="slider"></span>
        </label>
      </div>

      <div class="card-avatar-wrap">
        <div class="avatar-ring">
          <img src="${avatar}" alt="${escapeHtml(emp.name)}" onerror="this.src='https://api.dicebear.com/7.x/initials/svg?seed=${encodeURIComponent(emp.name)}'" />
        </div>
        <p class="emp-name">${escapeHtml(emp.name)}</p>
        <span class="emp-code-badge mono">Mã NV: #${escapeHtml(emp.employeeNo)}</span>
      </div>

      <div class="card-divider"></div>

      <div class="bio-counts">
        ${bioChip("face", emp.numOfFace, "Khuôn mặt")}
        ${bioChip("fingerprint", emp.numOfFingerprint, "Vân tay")}
        ${bioChip("card", emp.numOfCard, "Thẻ")}
      </div>

      <div class="card-row">
        <span class="card-row-label">Hiệu lực đến</span>
        <span>${formatDateVN(emp.validTo)}</span>
      </div>

      <div class="card-row" style="margin-bottom:0">
        <span class="card-row-label">Quyền LocalUI</span>
        <span class="right-badge">
          <span class="right-dot ${emp.localUIRight ? "on" : ""}"></span>
          ${emp.localUIRight ? "Được phép" : "Không"}
        </span>
      </div>

      <div class="card-actions">
        <button class="btn-icon btn-history" title="Lịch sử checkin/out" style="background: var(--primary); color: var(--surface); border: unset;" data-emp-no="${escapeHtml(emp.employeeNo)}">${ICONS.history}</button>
        <button class="btn-icon btn-edit" title="Sửa thông tin" style="background: var(--success); color: var(--surface); border: unset;" data-emp-no="${escapeHtml(emp.employeeNo)}">${ICONS.edit}</button>
        <button class="btn-icon danger btn-delete" title="Xóa nhân viên" style="background: var(--danger); color: var(--surface); border: unset;" data-emp-no="${escapeHtml(emp.employeeNo)}">${ICONS.trash}</button>
      </div>
    </div>
  `;
}

function renderPagination() {
  const el = document.getElementById("pagination");
  const hasNext = state.lastPageItemCount === PAGE_SIZE &&
                  (state.total <= state.lastPageItemCount || (state.page + 1) * PAGE_SIZE < state.total);
  const hasPrev = state.page > 0;

  document.getElementById("statPage").textContent = state.page + 1;
  document.getElementById("statTotal").textContent = state.total > 0 ? state.total : "-";

  if (state.lastPageItemCount === 0 && state.page === 0) { el.innerHTML = ""; return; }

  el.innerHTML = `
    <span>Trang ${state.page + 1}${state.total > state.lastPageItemCount ? ` / ~${Math.ceil(state.total / PAGE_SIZE)}` : ""}</span>
    <div class="pages">
      <button id="btnPrevPage" ${hasPrev ? "" : "disabled"}>‹ Trang trước</button>
      <button id="btnNextPage" ${hasNext ? "" : "disabled"}>Trang sau ›</button>
    </div>
  `;

  document.getElementById("btnPrevPage")?.addEventListener("click", () => {
    if (state.page > 0) { state.page--; loadEmployees(); }
  });
  document.getElementById("btnNextPage")?.addEventListener("click", () => {
    state.page++; loadEmployees();
  });
}

async function loadEmployees() {
  try {
    const result = await apiListUser(state.keyword, state.page, PAGE_SIZE);
    const items = result.items || [];
    state.total = result.total || items.length;
    state.lastPageItemCount = items.length;

    const listEl = document.getElementById("employeeList");
    const emptyEl = document.getElementById("emptyState");

    if (items.length === 0) {
      listEl.innerHTML = "";
      emptyEl.style.display = "block";
    } else {
      emptyEl.style.display = "none";
      listEl.innerHTML = items.map(renderEmployeeCard).join("");
      attachCardEvents();
    }

    renderPagination();
  } catch (err) {
    showToast(err.message, "error");
  }
}

function attachCardEvents() {
  document.querySelectorAll(".btn-edit").forEach(btn =>
    btn.addEventListener("click", () => openEditModal(btn.dataset.empNo)));

  document.querySelectorAll(".btn-delete").forEach(btn =>
    btn.addEventListener("click", () => openDeleteConfirm(btn.dataset.empNo)));

  document.querySelectorAll(".btn-history").forEach(btn =>
    btn.addEventListener("click", () => openAttendanceModal(btn.dataset.empNo)));

  document.querySelectorAll(".toggle-active").forEach(chk =>
    chk.addEventListener("change", () => onToggleActive(chk)));
}

async function onToggleActive(checkbox) {
  const empNo = checkbox.dataset.empNo;
  const enabled = checkbox.checked;
  checkbox.disabled = true;
  try {
    // ActiveUser (Modify) ghi de toan bo UserInfo nen phai lay ban ghi hien tai truoc,
    // chi doi field "enabled" (Valid.enable) roi gui lai nguyen ban ghi
    const current = await apiGetUser(empNo);
    if (!current) throw new Error("Không tìm thấy nhân viên trên thiết bị.");
    current.enabled = enabled;
    await apiUpdateUser(current);
    showToast(`Nhân viên #${empNo} đã ${enabled ? "có hiệu lực" : "bị khóa"}.`);
    const card = checkbox.closest(".employee-card");
    card.classList.toggle("disabled", !enabled);
    loadEmployees();
  } catch (err) {
    checkbox.checked = !enabled; // rollback UI
    showToast(err.message, "error");
  } finally {
    checkbox.disabled = false;
  }
}

// ============================================================================
// Search
// ============================================================================
const onSearchInput = debounce(() => {
  state.keyword = document.getElementById("searchInput").value.trim();
  state.page = 0;
  loadEmployees();
}, 400);

document.getElementById("searchInput").addEventListener("input", onSearchInput);
document.getElementById("btnRefresh").addEventListener("click", loadEmployees);

// ============================================================================
// Add / Edit modal
// ============================================================================
const employeeForm = {
  employeeNo: () => document.getElementById("f_employeeNo"),
  name: () => document.getElementById("f_name"),
  gender: () => document.getElementById("f_gender"),
  userType: () => document.getElementById("f_userType"),
  enabled: () => document.getElementById("f_enabled"),
  validFrom: () => document.getElementById("f_validFrom"),
  validTo: () => document.getElementById("f_validTo"),
  faceFile: () => document.getElementById("f_faceFile"),
  avatarPreview: () => document.getElementById("f_avatarPreview"),
  localUIRight: () => document.getElementById("f_localUIRight")
};

function resetEmployeeForm() {
  employeeForm.employeeNo().value = "";
  employeeForm.employeeNo().disabled = false;
  employeeForm.name().value = "";
  employeeForm.gender().value = "unknown";
  employeeForm.userType().value = "normal";
  employeeForm.enabled().value = "true";
  employeeForm.validFrom().value = toDateInputValue(new Date());
  employeeForm.validTo().value = toDateInputValue(new Date(new Date().setFullYear(new Date().getFullYear() + 10)));
  employeeForm.faceFile().value = "";
  employeeForm.avatarPreview().src = "https://api.dicebear.com/7.x/initials/svg?seed=NV";
  employeeForm.localUIRight().checked = false;
  state.pendingFaceFile = null;
  document.getElementById("err_employeeNo").textContent = "";
  document.getElementById("err_name").textContent = "";

  // Khoi sinh trac hoc (xoa khuon mat / them-xoa van tay) chi thao tac duoc tren nhan vien
  // DA TON TAI tren thiet bi, nen mac dinh an di khi mo modal them moi.
  document.getElementById("biometricSection").style.display = "none";
  state.capturedFingerNo = null;
  state.capturedFingerData = null;
  renderFingerprintSlots();
}

document.getElementById("btnAddEmployee").addEventListener("click", () => {
  state.editingEmployeeNo = null;
  resetEmployeeForm();
  document.getElementById("employeeModalTitle").textContent = "Thêm nhân viên";
  openModal("employeeModalOverlay");
});

async function openEditModal(employeeNo) {
  try {
    const emp = await apiGetUser(employeeNo);
    if (!emp) throw new Error("Không tìm thấy nhân viên.");

    state.editingEmployeeNo = employeeNo;
    resetEmployeeForm();

    employeeForm.employeeNo().value = emp.employeeNo;
    employeeForm.employeeNo().disabled = true; // khong cho doi ma khi sua
    employeeForm.name().value = emp.name;
    employeeForm.gender().value = emp.gender || "unknown";
    employeeForm.userType().value = emp.userType;
    employeeForm.enabled().value = String(emp.enabled);
    employeeForm.validFrom().value = toDateInputValue(emp.validFrom);
    employeeForm.validTo().value = toDateInputValue(emp.validTo);
    employeeForm.avatarPreview().src = emp.faceUrl ? proxyImageUrl(emp.faceUrl) : "https://api.dicebear.com/7.x/initials/svg?seed=NV";
    employeeForm.localUIRight().checked = emp.localUIRight;

    // Chi hien khoi sinh trac hoc khi sua (nhan vien da co san tren thiet bi)
    document.getElementById("biometricSection").style.display = "block";
    document.getElementById("bio_faceCount").textContent = emp.numOfFace ?? 0;
    document.getElementById("bio_fingerCount").textContent = emp.numOfFingerprint ?? 0;

    // Fetch registered fingerprints from device
    let registeredIds = [];
    try {
      const fps = await apiGetFingerPrints(employeeNo);
      registeredIds = fps.map(f => f.fingerPrintID);
    } catch (err) {
      console.warn("Lỗi lấy danh sách vân tay từ thiết bị:", err);
    }
    renderFingerprintSlots(registeredIds);

    document.getElementById("employeeModalTitle").textContent = `Sửa nhân viên #${emp.employeeNo}`;
    openModal("employeeModalOverlay");
  } catch (err) {
    showToast(err.message, "error");
  }
}

employeeForm.faceFile().addEventListener("change", () => {
  const file = employeeForm.faceFile().files[0];
  state.pendingFaceFile = file || null;
  if (file) {
    const reader = new FileReader();
    reader.onload = () => { employeeForm.avatarPreview().src = reader.result; };
    reader.readAsDataURL(file);
  }
});

// ---- Xóa khuôn mặt hiện có ----
document.getElementById("btnDeleteFace").addEventListener("click", async () => {
  if (!state.editingEmployeeNo) return;
  if (!confirm(`Xóa khuôn mặt hiện có của nhân viên #${state.editingEmployeeNo}?`)) return;

  try {
    await apiDeleteFace(state.editingEmployeeNo);
    showToast("Đã xóa khuôn mặt hiện có.");
    employeeForm.avatarPreview().src = "https://api.dicebear.com/7.x/initials/svg?seed=NV";
    document.getElementById("bio_faceCount").textContent = "0";
  } catch (err) {
    showToast(err.message, "error");
  }
});

// ---- Vân tay: render 10 hàng (ngón 1-10), moi hang tu quan ly chup/luu/xoa cho DUNG ngon do ----
function renderFingerprintSlots(registeredIds = []) {
  const container = document.getElementById("fingerprintSlots");
  let html = "";
  for (let n = 1; n <= 10; n++) {
    const isRegistered = registeredIds.includes(n);
    const labelStyle = isRegistered ? "color: var(--success); font-weight: 500;" : "";
    html += `
      <div class="fingerprint-slot-row" data-finger-no="${n}">
        <span class="fingerprint-slot-label" style="${labelStyle}">Ngón ${n}</span>
        <span class="fingerprint-slot-quality" id="fpQuality-${n}">${isRegistered ? 'Đã đăng ký ✓' : ''}</span>
        <div class="fingerprint-slot-actions">
          <button type="button" class="btn-icon fp-capture" data-finger-no="${n}" title="Chụp vân tay ngón ${n}" ${isRegistered ? 'style="display:none"' : 'style="background: var(--primary);color: var(--surface);border: unset;"'}>${ICONS.fingerprint2}</button>
          <button type="button" class="btn-icon fp-save" data-finger-no="${n}" title="Lưu vân tay ngón ${n} vừa chụp" style="display:none; background: var(--success); color: var(--surface); border: unset;">✓</button>
          <button type="button" class="btn-icon danger fp-delete" data-finger-no="${n}" title="Xóa vân tay ngón ${n}" ${isRegistered ? 'style="background: var(--danger);color: var(--surface);border: unset;"' : 'style="display:none"'}>${ICONS.trash2}</button>
        </div>
      </div>
    `;
  }
  container.innerHTML = html;

  container.querySelectorAll(".fp-capture").forEach(btn =>
    btn.addEventListener("click", () => onCaptureFingerprint(parseInt(btn.dataset.fingerNo, 10))));
  container.querySelectorAll(".fp-save").forEach(btn =>
    btn.addEventListener("click", () => onSaveFingerprint(parseInt(btn.dataset.fingerNo, 10))));
  container.querySelectorAll(".fp-delete").forEach(btn =>
    btn.addEventListener("click", () => onDeleteFingerprint(parseInt(btn.dataset.fingerNo, 10))));
}

async function onCaptureFingerprint(fingerNo) {
  const row = document.querySelector(`.fingerprint-slot-row[data-finger-no="${fingerNo}"]`);
  const qualityEl = document.getElementById(`fpQuality-${fingerNo}`);
  const btnCapture = row.querySelector(".fp-capture");
  const btnSave = row.querySelector(".fp-save");

  btnCapture.disabled = true;
  qualityEl.textContent = "Đang chụp...";

  try {
    const result = await apiCaptureFingerPrint(fingerNo);
    if (!result.fingerData) throw new Error("Không nhận được dữ liệu vân tay, thử chụp lại.");

    state.capturedFingerNo = fingerNo;
    state.capturedFingerData = result.fingerData;
    qualityEl.textContent = `Chất lượng: ${result.fingerPrintQuality}/100`;
    row.classList.add("has-quality");
    btnSave.style.display = "";
  } catch (err) {
    qualityEl.textContent = "";
    showToast(err.message, "error");
  } finally {
    btnCapture.disabled = false;
  }
}

async function onSaveFingerprint(fingerNo) {
  if (!state.editingEmployeeNo || state.capturedFingerNo !== fingerNo || !state.capturedFingerData) return;

  const row = document.querySelector(`.fingerprint-slot-row[data-finger-no="${fingerNo}"]`);
  const btnSave = row.querySelector(".fp-save");
  const btnDelete = row.querySelector(".fp-delete");
  const btnCapture = row.querySelector(".fp-capture");
  btnSave.disabled = true;

  try {
    await apiSetUpFingerPrint(state.editingEmployeeNo, state.capturedFingerData, fingerNo);
    showToast(`Đã lưu vân tay ngón ${fingerNo} cho nhân viên #${state.editingEmployeeNo}.`);

    document.getElementById(`fpQuality-${fingerNo}`).textContent = "Đã đăng ký ✓";
    const label = row.querySelector(".fingerprint-slot-label");
    if (label) {
      label.style.color = "var(--success)";
      label.style.fontWeight = "500";
    }
    btnDelete.style.display = ""; // Show delete button
    btnDelete.style.backgroundColor = "var(--danger)";
    btnDelete.style.color = "var(--surface)";
    btnSave.style.border = "unset";
    row.classList.remove("has-quality");
    btnSave.style.display = "none";
    btnCapture.style.display = "none";
    state.capturedFingerNo = null;
    state.capturedFingerData = null;

    const countEl = document.getElementById("bio_fingerCount");
    countEl.textContent = (parseInt(countEl.textContent, 10) || 0) + 1;
  } catch (err) {
    showToast(err.message, "error");
    btnSave.disabled = false;
  }
}

async function onDeleteFingerprint(fingerNo) {
  if (!state.editingEmployeeNo) return;
  if (!confirm(`Xóa vân tay ngón ${fingerNo} của nhân viên #${state.editingEmployeeNo}?`)) return;

  const row = document.querySelector(`.fingerprint-slot-row[data-finger-no="${fingerNo}"]`);

  try {
    await apiDeleteFingerPrint(state.editingEmployeeNo, fingerNo);
    showToast(`Đã xóa vân tay ngón ${fingerNo}.`);

    document.getElementById(`fpQuality-${fingerNo}`).textContent = "";
    const label = row.querySelector(".fingerprint-slot-label");
    if (label) {
      label.style.color = "";
      label.style.fontWeight = "";
    }
    row.querySelector(".fp-delete").style.display = "none"; // Hide delete button

    const countEl = document.getElementById("bio_fingerCount");
    countEl.textContent = Math.max(0, (parseInt(countEl.textContent, 10) || 0) - 1);
  } catch (err) {
    showToast(err.message, "error");
  }
}

document.getElementById("btnSaveEmployee").addEventListener("click", async () => {
  const employeeNo = employeeForm.employeeNo().value.trim();
  const name = employeeForm.name().value.trim();

  document.getElementById("err_employeeNo").textContent = "";
  document.getElementById("err_name").textContent = "";

  let hasError = false;
  if (!employeeNo) { document.getElementById("err_employeeNo").textContent = "Vui lòng nhập mã nhân viên."; hasError = true; }
  if (!name) { document.getElementById("err_name").textContent = "Vui lòng nhập tên nhân viên."; hasError = true; }
  if (hasError) return;

  const personInfo = {
    employeeNo,
    name,
    gender: employeeForm.gender().value,
    userType: employeeForm.userType().value,
    enabled: employeeForm.enabled().value === "true",
    validFrom: employeeForm.validFrom().value,
    validTo: employeeForm.validTo().value,
    localUIRight: employeeForm.localUIRight().checked
  };

  const saveBtn = document.getElementById("btnSaveEmployee");
  saveBtn.disabled = true;
  saveBtn.textContent = "Đang lưu...";

  try {
    if (state.editingEmployeeNo) {
      await apiUpdateUser(personInfo);
    } else {
      await apiCreateUser(personInfo);
    }

    // Neu co chon anh moi thi upload rieng qua UploadFace (multipart) sau khi da co ban ghi UserInfo
    if (state.pendingFaceFile) {
      await apiUploadFace(employeeNo, state.pendingFaceFile);
    }

    showToast(state.editingEmployeeNo ? `Đã cập nhật nhân viên #${employeeNo}.` : `Đã thêm nhân viên #${employeeNo}.`);
    closeModal("employeeModalOverlay");
    loadEmployees();
  } catch (err) {
    showToast(err.message, "error");
  } finally {
    saveBtn.disabled = false;
    saveBtn.textContent = "Lưu nhân viên";
  }
});

// ============================================================================
// Delete confirm
// ============================================================================
function openDeleteConfirm(employeeNo) {
  state.deletingEmployeeNo = employeeNo;
  document.getElementById("confirmDeleteText").textContent =
    `Toàn bộ dữ liệu vân tay, khuôn mặt, thẻ của nhân viên #${employeeNo} sẽ bị xóa khỏi thiết bị. Hành động này không thể hoàn tác.`;
  openModal("confirmModalOverlay");
}

document.getElementById("btnConfirmDelete").addEventListener("click", async () => {
  try {
    await apiDeleteUser(state.deletingEmployeeNo);
    showToast(`Đã xóa nhân viên #${state.deletingEmployeeNo}.`);
    closeModal("confirmModalOverlay");
    loadEmployees();
  } catch (err) {
    showToast(err.message, "error");
  }
});

// ============================================================================
// Attendance history modal (SearchAcsEventUser)
// ============================================================================
async function openAttendanceModal(employeeNo) {
  state.attendanceEmployeeNo = employeeNo;

  const today = toDateInputValue(new Date());
  const weekAgo = toDateInputValue(new Date(new Date().setDate(new Date().getDate() - 7)));
  document.getElementById("att_fromDate").value = weekAgo;
  document.getElementById("att_toDate").value = today;

  try {
    const emp = await apiGetUser(employeeNo);
    document.getElementById("attendanceEmpInfo").innerHTML = emp ? `
      <img src="${emp.faceUrl ? proxyImageUrl(emp.faceUrl) : "https://api.dicebear.com/7.x/initials/svg?seed=" + encodeURIComponent(emp.name)}" alt="${escapeHtml(emp.name)}" />
      <div>
        <div style="font-weight:600; font-size:14px">${escapeHtml(emp.name)}</div>
        <div class="mono" style="font-size:12px; color:var(--ink-soft)">#${escapeHtml(emp.employeeNo)}</div>
      </div>
    ` : `<span class="mono">#${escapeHtml(employeeNo)}</span>`;
  } catch { /* van cho phep xem lich su neu khong lay duoc thong tin nhan vien */ }

  openModal("attendanceModalOverlay");
  await loadAttendance();
}

async function loadAttendance() {
  const fromDate = document.getElementById("att_fromDate").value;
  const toDate = document.getElementById("att_toDate").value;
  const tbody = document.getElementById("attendanceTableBody");
  const emptyEl = document.getElementById("attendanceEmpty");

  try {
    const startTime = toIsapiDateTime(fromDate, false);
    const endTime = toIsapiDateTime(toDate, true);
    const items = await apiSearchAttendance(state.attendanceEmployeeNo, startTime, endTime);

    if (!items || items.length === 0) {
      tbody.innerHTML = "";
      emptyEl.style.display = "block";
      return;
    }
    emptyEl.style.display = "none";
    tbody.innerHTML = items.map(r => {
      const pictureUrl = r.pictureURL || r.pictureUrl || "";
      const proxied = pictureUrl ? proxyImageUrl(pictureUrl) : "";
      const typeClass = `type-${r.userType}`;
      const verify = r.currentVerifyMode === "faceOrFpOrCardOrPw" && r.pictureURL !== "" ? "Xác thực bằng Khuôn mặt" : "Xác thực bằng Vân tay"
      return `
        <tr>
          <td class="mono">${formatDateTimeVN(r.time)}</td>
          <td class="mono"><span class="type-badge ${typeClass}">${escapeHtml(r.userType)}</span></td>
          <td><span class="verify-tag">${escapeHtml(verify || "-")}</span></td>
          <td>${proxied ? `<img src="${proxied}" alt="checkin" style="width:36px;height:36px;border-radius:8px;object-fit:cover" />` : "-"}</td>
        </tr>
      `;
    }).join("");
  } catch (err) {
    showToast(err.message, "error");
  }
}

document.getElementById("btnFilterAttendance").addEventListener("click", loadAttendance);

// ============================================================================
// Init
// ============================================================================
loadEmployees();
