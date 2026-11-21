const dgram = require('dgram');
const http = require('http');
const os = require('os');
const readline = require('readline');

function getAllLocalIPs() {
  const interfaces = os.networkInterfaces();
  const list = [];
  for (const name of Object.keys(interfaces)) {
    for (const iface of interfaces[name]) {
      if (iface.family === 'IPv4' && !iface.internal) {
        list.push({ name, address: iface.address });
      }
    }
  }
  return list;
}

function parseDomain(msg) {
  let offset = 12;
  const parts = [];
  while (offset < msg.length && msg[offset] !== 0) {
    const len = msg[offset];
    parts.push(msg.slice(offset + 1, offset + 1 + len).toString('utf8'));
    offset += len + 1;
  }
  return { domain: parts.join('.').toLowerCase(), endOffset: offset + 5 };
}

function startServers(LOCAL_IP) {
  const IP_BYTES = LOCAL_IP.split('.').map(Number);

  const dns = dgram.createSocket('udp4');
  dns.on('message', (msg, rinfo) => {
    const { domain, endOffset } = parseDomain(msg);
    const question = msg.slice(12, endOffset);

    if (domain.includes('ctest') || domain.includes('conntest') || domain.includes('nintendo')) {
      const res = Buffer.concat([
        msg.slice(0, 2),
        Buffer.from([0x81, 0x80, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00]),
        question,
        Buffer.from([0xc0, 0x0c, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00, 0x00, 0x3c, 0x00, 0x04, ...IP_BYTES])
      ]);
      dns.send(res, rinfo.port, rinfo.address);
      console.log(`[ALLOWED] ${domain} (from ${rinfo.address}) -> ${LOCAL_IP}`);
    } else {
      const res = Buffer.concat([
        msg.slice(0, 2),
        Buffer.from([0x81, 0x83, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00]),
        question
      ]);
      dns.send(res, rinfo.port, rinfo.address);
      console.log(`[BLOCKED] ${domain} (from ${rinfo.address})`);
    }
  });

  dns.bind(53, LOCAL_IP);

  const httpServer = http.createServer((req, res) => {
    console.log(`[HTTP-CHECK] Request from Switch: ${req.url}`);
    res.writeHead(200, {
      'Content-Type': 'text/plain',
      'X-Organization': 'Nintendo'
    });
    res.end('ok');
  });

  httpServer.on('error', (err) => {
    console.log(`[ERROR PORT 80] ${err.message}`);
  });

  httpServer.listen(80, LOCAL_IP, () => {
    console.log('\n====================================================');
    console.log(`[NEW VERSION ACTIVE] (Switch 2 ctest allowed!)`);
    console.log(`Server running on IP: ${LOCAL_IP}`);
    console.log('====================================================\n');
  });
}

const ips = getAllLocalIPs();
console.log('Available network adapters on your PC:');
ips.forEach((item, index) => {
  console.log(`  [${index + 1}] ${item.address}  (${item.name})`);
});

const rl = readline.createInterface({ input: process.stdin, output: process.stdout });
rl.question('\nPlease enter the number of your network adapter: ', (answer) => {
  const choice = parseInt(answer, 10) - 1;
  rl.close();
  if (choice >= 0 && choice < ips.length) {
    startServers(ips[choice].address);
  }
});
