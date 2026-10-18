// Build the sidebar + topbar around <main id="page"> and provide shared helpers for every page.
const fmtMoney = n => Number(n).toLocaleString('en-US') + ' VND';
const fmtShort = n => n >= 1e9 ? (n/1e9).toFixed(1) + ' billion' : Math.round(n/1e6) + ' million';
const badge = (cls, text) => `<span class="badge ${cls}">${text}</span>`;
const tierBadge = t => badge({'Standard':'g-standard','Silver':'g-silver','Gold':'g-gold','Diamond':'g-diamond'}[t], t);
const scheduleBadge = s => badge({'Overdue':'b-danger','Today':'b-warning','Upcoming':'b-info'}[s], s);
const priorityBadge = p => badge({1:'b-danger',2:'b-warning',3:'b-neutral'}[p], {1:'High',2:'Medium',3:'Low'}[p]);

function toast(msg, type='info'){
  const c = {info:'var(--primary)', ok:'var(--success)', err:'var(--danger)'}[type];
  const el = document.createElement('div');
  el.className = 'fixed top-4 right-4 z-50 bg-white card px-4 py-3 text-sm shadow-lg';
  el.style.borderLeft = `4px solid ${c}`; el.textContent = msg;
  document.body.appendChild(el); setTimeout(() => el.remove(), 3500);
}

const USER = JSON.parse(localStorage.getItem('ccm_user') || 'null');
const PAGE = document.body.dataset.page;
if (!USER) location.replace('login.html');

const ALL = ['Manager','Sales','Support'];
const NAV = [
  {k:'dashboard',   t:'Dashboard',      i:'fa-gauge-high',    h:'dashboard.html',    r:ALL, built:true},
  {k:'customers',   t:'Customers',      i:'fa-users',         h:'customers.html',    r:ALL, built:true},
  {k:'contracts',   t:'Contracts',      i:'fa-file-contract', h:'contracts.html',    r:ALL},
  {k:'contacts',    t:'Contacts',       i:'fa-phone',         h:'contacts.html',     r:ALL},
  {k:'care',        t:'Care',           i:'fa-heart',         h:'care.html',         r:ALL},
  {k:'feedback',    t:'Feedback',       i:'fa-comments',      h:'feedback.html',     r:['Manager','Support']},
  {k:'reports',     t:'Reports',        i:'fa-chart-column',  h:'reports.html',      r:ALL},
  {k:'administration', t:'Administration', i:'fa-gear',       h:'administration.html', r:['Manager']}
];

document.addEventListener('DOMContentLoaded', () => {
  if (!USER) return;
  const links = NAV.filter(n => n.r.includes(USER.role)).map(n =>
    `<a href="${n.built ? n.h : '#'}" data-soon="${!n.built}" class="nav-link ${n.k === PAGE ? 'active' : ''}"><i class="fa-solid ${n.i}"></i><span>${n.t}</span></a>`).join('');
  const initials = USER.name.split(' ').slice(-2).map(w => w[0]).join('');
  document.body.insertAdjacentHTML('afterbegin', `
  <aside id="side" class="fixed inset-y-0 left-0 z-40 w-64 bg-side flex flex-col -translate-x-full lg:translate-x-0 transition-transform">
    <div class="h-16 flex items-center gap-3 px-5 text-white font-bold border-b border-white/10">
      <i class="fa-solid fa-handshake-angle text-xl"></i><span>Customer Care Management</span></div>
    <nav class="flex-1 p-3 space-y-1 overflow-y-auto">${links}</nav>
    <div class="p-4 border-t border-white/10 text-sm" style="color:var(--text-on-dark)">
      <div class="font-semibold text-white">${USER.name}</div><div>${USER.role}</div></div>
  </aside>
  <div class="lg:pl-64 min-h-screen flex flex-col">
    <header class="h-16 bg-white border-b border-line flex items-center gap-3 px-4 sticky top-0 z-30">
      <button id="menuBtn" class="lg:hidden btn" aria-label="Open menu"><i class="fa-solid fa-bars"></i></button>
      <form id="searchForm" class="flex-1 max-w-md relative">
        <i class="fa-solid fa-magnifying-glass absolute left-3 top-1/2 -translate-y-1/2 text-muted"></i>
        <input id="searchBox" class="input pl-9" placeholder="Search customers by name, email or phone" />
      </form>
      <div class="ml-auto flex items-center gap-3">
        <button class="btn relative" aria-label="Notifications" onclick="toast('4 items need attention: 2 contracts expiring soon, 2 overdue feedback items')">
          <i class="fa-solid fa-bell"></i>
          <span class="absolute -top-1 -right-1 bg-danger text-white text-[11px] rounded-full w-5 h-5 grid place-items-center">4</span></button>
        <div class="w-9 h-9 rounded-full bg-primary text-white grid place-items-center text-sm font-bold">${initials}</div>
        <button id="logout" class="btn" aria-label="Sign out"><i class="fa-solid fa-right-from-bracket"></i></button>
      </div>
    </header>
    <div id="slot" class="p-4 lg:p-6 w-full max-w-[1440px] mx-auto"></div>
  </div>`);
  document.getElementById('slot').appendChild(document.getElementById('page'));

  const side = document.getElementById('side');
  document.getElementById('menuBtn').onclick = () => side.classList.toggle('-translate-x-full');
  document.getElementById('logout').onclick = () => { localStorage.removeItem('ccm_user'); location.href = 'login.html'; };
  document.querySelectorAll('[data-soon="true"]').forEach(a => a.onclick = e => { e.preventDefault(); toast('This page arrives in a later batch'); });
  document.getElementById('searchForm').onsubmit = e => {
    e.preventDefault(); location.href = 'customers.html?q=' + encodeURIComponent(document.getElementById('searchBox').value);
  };
});
