// Highlights the code, and copies the install command
if (window.hljs) {
  hljs.highlightAll();
}

document.querySelectorAll('[data-copy]').forEach(function (button) {
  button.addEventListener('click', function () {
    navigator.clipboard.writeText(button.getAttribute('data-copy')).then(function () {
      button.classList.add('copied');
      setTimeout(function () { button.classList.remove('copied'); }, 1500);
    });
  });
});
