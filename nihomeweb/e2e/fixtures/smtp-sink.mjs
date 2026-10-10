// Test-only SMTP receiver. No message is relayed to the internet.
import { createServer as createHttpServer } from "node:http";
import { createServer as createTcpServer } from "node:net";

const messages = [];

createHttpServer((request, response) => {
  if (request.method !== "GET" || request.url !== "/messages") {
    response.writeHead(404).end();
    return;
  }
  response.writeHead(200, { "Content-Type": "application/json" });
  response.end(JSON.stringify(messages));
}).listen(8025, "0.0.0.0");

createTcpServer((socket) => {
  let pending = "";
  let mode = "command";
  let recipient = "";
  let lines = [];
  socket.setEncoding("utf8");
  socket.write("220 E2E SMTP sink ready\r\n");

  socket.on("data", (chunk) => {
    pending += chunk;
    let lineEnd;
    while ((lineEnd = pending.indexOf("\r\n")) !== -1) {
      const line = pending.slice(0, lineEnd);
      pending = pending.slice(lineEnd + 2);
      if (mode === "data") {
        if (line === ".") {
          messages.push({ recipient, raw: `${lines.join("\r\n")}\r\n` });
          lines = [];
          mode = "command";
          socket.write("250 Message accepted\r\n");
        } else {
          lines.push(line.startsWith("..") ? line.slice(1) : line);
        }
        continue;
      }
      if (mode === "username") {
        mode = "password";
        socket.write("334 UGFzc3dvcmQ6\r\n");
      } else if (mode === "password") {
        mode = "command";
        socket.write("235 Authentication successful\r\n");
      } else if (/^EHLO\b/i.test(line)) {
        socket.write("250-smtp-sink\r\n250 AUTH LOGIN\r\n");
      } else if (/^HELO\b/i.test(line)) {
        socket.write("250 smtp-sink\r\n");
      } else if (/^AUTH LOGIN\b/i.test(line)) {
        mode = "username";
        socket.write("334 VXNlcm5hbWU6\r\n");
      } else if (/^MAIL FROM:/i.test(line)) {
        recipient = "";
        lines = [];
        socket.write("250 Sender accepted\r\n");
      } else if (/^RCPT TO:/i.test(line)) {
        recipient = line.slice(8).trim().replace(/[<>]/g, "");
        socket.write("250 Recipient accepted\r\n");
      } else if (/^DATA$/i.test(line)) {
        mode = "data";
        socket.write("354 End data with <CRLF>.<CRLF>\r\n");
      } else if (/^RSET$/i.test(line)) {
        recipient = "";
        lines = [];
        socket.write("250 Reset\r\n");
      } else if (/^QUIT$/i.test(line)) {
        socket.end("221 Goodbye\r\n");
      } else {
        socket.write("502 Command not supported\r\n");
      }
    }
  });
}).listen(1025, "0.0.0.0");
