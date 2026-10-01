mergeInto(LibraryManager.library, {
  StoryIQOpenGames: function () {
    // Keep the Unity iframe and its current story alive in the original tab.
    var url = 'https://iqgamesonline.com/?iqreturn=1&game=StoryIQ';
    var tab = window.open(url, '_blank');
    if (tab) { tab.focus(); return; }
    // A real link provides a second user gesture if a browser blocks window.open.
    var existing = document.getElementById('storyiq-games-link');
    if (existing) { existing.focus(); return; }
    var panel = document.createElement('div');
    panel.style.cssText = 'position:fixed;inset:0;z-index:9999;background:rgba(0,0,0,.9);display:flex;align-items:center;justify-content:center;gap:24px;flex-direction:column;font:20px system-ui;color:white';
    var link = document.createElement('a');
    link.id = 'storyiq-games-link'; link.href = url; link.target = '_blank';
    link.textContent = 'OPEN ALL IQ GAMES';
    link.style.cssText = 'background:white;color:black;padding:16px 24px;text-decoration:none';
    var close = document.createElement('button');
    close.textContent = 'RETURN TO STORYIQ';
    close.style.cssText = 'padding:16px 24px;font:inherit';
    close.onclick = function () { panel.remove(); document.getElementById('unity-canvas').focus(); };
    panel.appendChild(link); panel.appendChild(close); document.body.appendChild(panel); link.focus();
  }
});
