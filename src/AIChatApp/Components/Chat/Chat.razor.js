export function submitOnEnter(formElem) {
  var isSubmitting = false;

  formElem.addEventListener("keydown", (e) => {
    if (
      e.key === "Enter" &&
      !e.ctrlKey &&
      !e.shiftKey &&
      !e.altKey &&
      !e.metaKey
    ) {
      e.preventDefault();

      if (isSubmitting) return;
      isSubmitting = true;

      e.srcElement.dispatchEvent(new Event("change", { bubbles: true }));
      formElem.requestSubmit();

      setTimeout(() => {
        isSubmitting = false;
      }, 1000); // Debounce window
    }
  });
}

export function autoResizeTextarea(textareaId, minRows = 3, maxRows = 5) {
  const textarea = document.getElementById(textareaId);
  if (!textarea) return;

  const adjustTextareaRows = () => {
    const lineHeight = parseFloat(getComputedStyle(textarea).lineHeight);
    textarea.rows = minRows;
    const currentRows = Math.floor(textarea.scrollHeight / lineHeight);
    textarea.rows = Math.min(currentRows, maxRows);
  };

  textarea.addEventListener("input", adjustTextareaRows);
}

export function scrollToBottom(element) {
  element?.scrollIntoView({ behavior: "smooth", block: "end" });
}

export function focusTextarea(formElement) {
  formElement?.querySelector("textarea")?.focus();
}
