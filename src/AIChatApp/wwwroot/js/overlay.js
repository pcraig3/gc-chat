(function ($, wb) {
  "use strict";

  // calculate the width of the scroll bar
  function getScrollbarWidth() {
    return window.innerWidth - document.documentElement.clientWidth;
  }

  $(document).on("click", ".open-overlay-btn", function () {
    const overlayId = $(this).data("overlay-id");
    const scrollbarWidth = getScrollbarWidth();
    if (overlayId) {
      $("#" + overlayId).trigger("open.wb-overlay");

      // set padding equal to scroll bar width to prevent layout shift
      $("body").css("padding-right", `${scrollbarWidth}px`);
    }
  });

  $(document).on("closed.wb-overlay", function () {
    $("body").css("padding-right", ""); // reset padding when overlay closes
  });
})(jQuery, wb);

// Exported for Blazor
export function wbInitOverlay(overlayId) {
  const $overlay = $("#" + overlayId);
  if ($overlay.length) {
    $overlay.trigger("wb-init.wb-overlay");
  }
}

export function wbCloseOverlay(overlayId) {
  const $body = $("body");
  const $overlay = $body.find("#" + overlayId);

  if ($overlay.length) {
    $overlay.trigger("close.wb-overlay");
  }

  $body.removeClass("wb-overlay-dlg");
  $body.css("padding-right", "");
}
