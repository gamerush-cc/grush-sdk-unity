var GRushAnalyticsLibrary = {
  // Synchronous: returns 1 when GameRush took the event, 0 otherwise. Never throws into the game.
  // With no kind it answers whether the game runs inside GameRush.
  GRushJsAnalytics: function (kindPtr, paramsPtr) {
    try {
      var api =
        typeof window === "undefined"
          ? null
          : window.GRushAnalytics || window.GameRushAnalytics || null;
      if (!api) return 0;
      var kind = kindPtr ? UTF8ToString(kindPtr) : "";
      if (!kind) return api.isAvailable() === true ? 1 : 0;
      var raw = paramsPtr ? UTF8ToString(paramsPtr) : "";
      return api.log(kind, raw ? JSON.parse(raw) : undefined) === true ? 1 : 0;
    } catch (error) {
      return 0;
    }
  },
};

mergeInto(LibraryManager.library, GRushAnalyticsLibrary);
