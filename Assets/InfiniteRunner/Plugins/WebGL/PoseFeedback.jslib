mergeInto(LibraryManager.library, {
  RunnerPoseFeedback: function (jsonPtr) {
    if (typeof window.runnerPoseFeedback === "function") {
      window.runnerPoseFeedback(JSON.parse(UTF8ToString(jsonPtr)));
    }
  }
});
