// Run after DOM is parsed
document.addEventListener('DOMContentLoaded', () => {

  // Copy buttons
  document.querySelectorAll('.copy').forEach(btn => {
    btn.addEventListener('click', async () => {
      const textToCopy = btn.dataset.copy || '';
      try {
        await navigator.clipboard.writeText(textToCopy);
        const original = btn.textContent;
        btn.textContent = 'Copied!';
        setTimeout(() => (btn.textContent = original), 1200);
      } catch (err) {
        console.error('Copy failed:', err);
        btn.textContent = 'Copy failed';
        setTimeout(() => (btn.textContent = 'Copy'), 1500);
      }
    });
  });

  // Mobile menu toggle
  const toggle = document.querySelector('.menu-toggle');
  const nav = document.querySelector('.topnav');
  const navLinks = document.querySelectorAll('#nav-links a');

  if (toggle && nav) {
    toggle.addEventListener('click', () => {
      const open = nav.classList.toggle('open');
      toggle.setAttribute('aria-expanded', String(open));
    });

    // Close on link click
    navLinks.forEach(a => a.addEventListener('click', () => {
      if (nav.classList.contains('open')) {
        nav.classList.remove('open');
        toggle.setAttribute('aria-expanded', 'false');
      }
    }));

    // Close on outside click
    document.addEventListener('click', (e) => {
      if (nav.classList.contains('open') &&
          !nav.contains(e.target) &&
          !toggle.contains(e.target)) {
        nav.classList.remove('open');
        toggle.setAttribute('aria-expanded', 'false');
      }
    });

    // Close on Escape
    document.addEventListener('keydown', (e) => {
      if (e.key === 'Escape' && nav.classList.contains('open')) {
        nav.classList.remove('open');
        toggle.setAttribute('aria-expanded', 'false');
        toggle.focus();
      }
    });
  }

});

