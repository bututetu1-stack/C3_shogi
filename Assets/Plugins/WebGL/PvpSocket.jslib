// 対人戦の通信（ブラウザ版）。C# の System.Net.WebSockets はブラウザで動かないので、ブラウザの WebSocket を使う。
// 受け取ったものは SendMessage で PvpNet の JsOpen・JsMessage・JsClose に渡す（"接続の番号|本文"。古い接続の分は C# 側で捨てる）
mergeInto(LibraryManager.library, {
  PvpSocketOpen: function (urlPtr, receiverPtr) {
    var url = UTF8ToString(urlPtr);
    var receiver = UTF8ToString(receiverPtr);
    var store = Module.c3Sockets || (Module.c3Sockets = { next: 1, map: {} });
    var id = store.next++;
    var tell = function (method, text) { SendMessage(receiver, method, id + '|' + text); };
    var ws;
    try { ws = new WebSocket(url); }
    catch (e) { setTimeout(function () { tell('JsClose', 'つながらない: ' + e.message); }, 0); return id; }
    var opened = false;
    store.map[id] = ws;
    ws.onopen = function () { opened = true; tell('JsOpen', ''); };
    ws.onmessage = function (e) { if (typeof e.data === 'string') tell('JsMessage', e.data); };
    ws.onclose = function (e) {
      delete store.map[id];
      tell('JsClose', opened ? '切れた' : 'つながらない（' + e.code + '）');
    };
    return id;
  },

  PvpSocketSend: function (id, textPtr) {
    var store = Module.c3Sockets;
    var ws = store && store.map[id];
    if (!ws || ws.readyState !== 1) return 0;
    ws.send(UTF8ToString(textPtr));
    return 1;
  },

  PvpSocketClose: function (id) {
    var store = Module.c3Sockets;
    var ws = store && store.map[id];
    if (!ws) return;
    delete store.map[id];
    ws.onopen = ws.onmessage = ws.onclose = null;
    try { ws.close(); } catch (e) { }
  }
});
