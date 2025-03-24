export function initializeWetTables() {
  if (window.jQuery) {
    window.jQuery(".wb-tables").trigger("wb-init.wb-tables");
  } else {
    console.warn("jQuery not found: WET tables not initialized.");
  }
}

export function patchPaginationLinks() {
  const isWetLink = (el) =>
    el?.tagName === "A" &&
    el.getAttribute("href")?.startsWith("#") &&
    el.getAttribute("role") === "link";

  const preventLinkNavigation = (e) => {
    const target = e.target.closest("a");
    if (isWetLink(target)) {
      e.preventDefault();
    }
  };

  document.addEventListener("click", preventLinkNavigation, true);
  document.addEventListener(
    "keydown",
    (e) => {
      if (isWetLink(e.target) && (e.key === "Enter" || e.key === " ")) {
        e.preventDefault();
        // Simulate a real click to trigger DataTables pagination
        e.target.click();
      }
    },
    true
  );
}
