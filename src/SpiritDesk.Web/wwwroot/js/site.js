(() => {
  const clock = document.getElementById("liveClock");
  if (!clock) {
    return;
  }

  const updateClock = () => {
    const now = new Date();
    const hours = String(now.getHours()).padStart(2, "0");
    const minutes = String(now.getMinutes()).padStart(2, "0");
    clock.textContent = `${hours}:${minutes}`;
  };

  updateClock();
  window.setInterval(updateClock, 1000);
})();
