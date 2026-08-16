var GRushBridgeLibrary = {
  $GRushBridge: {
    onRpc: 0,
    onNet: 0,
    unbind: null,

    api: function (name) {
      return typeof window === "undefined" ? null : window[name] || null;
    },

    respond: function (requestId, ok, text) {
      if (!GRushBridge.onRpc) return;
      var size = lengthBytesUTF8(text) + 1;
      var ptr = _malloc(size);
      stringToUTF8(text, ptr, size);
      var callback = GRushBridge.onRpc;
      {{{ makeDynCall("viiii", "callback") }}}(requestId, ok, ptr, size - 1);
      _free(ptr);
    },

    fail: function (requestId, error) {
      var code = error && typeof error.code === "string" ? error.code : "internal";
      var message = error && error.message ? String(error.message) : "GameRush API call failed.";
      GRushBridge.respond(requestId, 0, JSON.stringify({ code: code, message: message }));
    },

    emit: function (kind, from, channel, seq, bytes, text) {
      if (!GRushBridge.onNet) return;
      var size = bytes ? bytes.length : lengthBytesUTF8(text) + 1;
      var ptr = _malloc(size > 0 ? size : 1);
      if (bytes) {
        HEAPU8.set(bytes, ptr);
      } else {
        stringToUTF8(text, ptr, size);
        size -= 1;
      }
      var callback = GRushBridge.onNet;
      {{{ makeDynCall("viiiiii", "callback") }}}(kind, from, channel, seq, ptr, size);
      _free(ptr);
    },

    bind: function (room) {
      if (GRushBridge.unbind) GRushBridge.unbind();
      var offs = [
        room.on("message", function (m) {
          GRushBridge.emit(1, m.from, m.channel === "unreliable" ? 1 : 0, m.seq, new Uint8Array(m.payload), null);
        }),
        room.on("peerjoin", function (peer) {
          GRushBridge.emit(2, peer.index, 0, 0, null, JSON.stringify(peer));
        }),
        room.on("peerleave", function (event) {
          GRushBridge.emit(3, event.index, 0, 0, null, "");
        }),
        room.on("host", function (event) {
          GRushBridge.emit(4, event.index, 0, 0, null, "");
        }),
        room.on("transportchange", function (event) {
          GRushBridge.emit(5, 0, 0, 0, null, String(event.transport || ""));
        }),
        room.on("close", function (event) {
          GRushBridge.emit(6, 0, 0, 0, null, String(event.reason || ""));
          if (GRushBridge.unbind) GRushBridge.unbind();
        }),
      ];
      GRushBridge.unbind = function () {
        GRushBridge.unbind = null;
        for (var i = 0; i < offs.length; i++) {
          if (typeof offs[i] === "function") offs[i]();
        }
      };
      return room.describe();
    },

    invoke: function (method, params) {
      var player = GRushBridge.api("GRushPlayer");
      var net = GRushBridge.api("GRushNet");
      if (method === "player.getSelf" && player) return player.getSelf();
      if (method === "player.requestProfile" && player) return player.requestProfile();
      if (method === "player.revokeProfile" && player) return player.revokeProfile();
      if (method === "net.join" && net) return net.join(params).then(GRushBridge.bind);
      if (method === "net.leave" && net) {
        if (GRushBridge.unbind) GRushBridge.unbind();
        return net.room ? net.room.leave() : Promise.resolve(null);
      }
      return Promise.reject({
        code: "unsupportedMethod",
        message: "GameRush does not expose " + method + " here.",
      });
    },
  },

  GRushJsPresent: function () {
    return GRushBridge.api("GRushInfo") && GRushBridge.api("GRushNet") && GRushBridge.api("GRushPlayer") ? 1 : 0;
  },
  GRushJsPresent__deps: ["$GRushBridge"],

  GRushJsProtocolVersion: function () {
    var info = GRushBridge.api("GRushInfo");
    return info && typeof info.protocolVersion === "number" ? info.protocolVersion : 0;
  },
  GRushJsProtocolVersion__deps: ["$GRushBridge"],

  GRushJsInit: function (onRpc, onNet) {
    GRushBridge.onRpc = onRpc;
    GRushBridge.onNet = onNet;
  },
  GRushJsInit__deps: ["$GRushBridge", "malloc", "free"],

  GRushJsCall: function (requestId, methodPtr, paramsPtr) {
    var method = UTF8ToString(methodPtr);
    var raw = paramsPtr ? UTF8ToString(paramsPtr) : "";
    var params;
    try {
      params = raw ? JSON.parse(raw) : undefined;
    } catch (error) {
      GRushBridge.fail(requestId, { code: "invalidParams", message: "Params were not JSON." });
      return;
    }
    try {
      GRushBridge.invoke(method, params).then(
        function (value) {
          GRushBridge.respond(requestId, 1, JSON.stringify(value === undefined ? null : value));
        },
        function (error) {
          GRushBridge.fail(requestId, error);
        },
      );
    } catch (error) {
      GRushBridge.fail(requestId, error);
    }
  },
  GRushJsCall__deps: ["$GRushBridge", "malloc", "free"],

  GRushJsSend: function (dataPtr, length, channel, to) {
    var net = GRushBridge.api("GRushNet");
    if (!net || !net.room) return;
    try {
      net.room.send(HEAPU8.slice(dataPtr, dataPtr + length), {
        channel: channel === 1 ? "unreliable" : "reliable",
        to: to,
      });
    } catch (error) {}
  },
  GRushJsSend__deps: ["$GRushBridge"],
};

mergeInto(LibraryManager.library, GRushBridgeLibrary);
