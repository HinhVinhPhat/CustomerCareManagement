// Map the colours from css/style.css into Tailwind: bg-primary, text-muted, border-line...
tailwind.config = { theme: { extend: {
  fontFamily: { sans: ['"Be Vietnam Pro"', 'Inter', 'system-ui', 'sans-serif'] },
  colors: { primary:'var(--primary)', phover:'var(--primary-hover)', psoft:'var(--primary-soft)',
    page:'var(--bg-page)', side:'var(--bg-sidebar)', line:'var(--border)', ink:'var(--text)', muted:'var(--text-muted)',
    success:'var(--success)', info:'var(--info)', warning:'var(--warning)', danger:'var(--danger)' }
}}};
