/* Mobile-sized browser smoke. Run only through test_docker_preview.py --browser. */
const assert = require('node:assert/strict');
const crypto = require('node:crypto');
const fs = require('node:fs');
const https = require('node:https');
const {execFileSync} = require('node:child_process');
const {chromium} = require(process.env.PLAYWRIGHT_MODULE || 'playwright');
const redactions = [];

(async () => {
  const origin = process.env.TRONO_PREVIEW_URL;
  const certificate = process.env.TRONO_PREVIEW_CA;
  const target = new URL(origin);
  assert(target.protocol === 'https:' && ['localhost', '127.0.0.1'].includes(target.hostname));
  const ca = fs.readFileSync(certificate);
  const publicKey = execFileSync('openssl', ['x509', '-in', certificate, '-pubkey', '-noout']);
  const pin = crypto.createHash('sha256').update(crypto.createPublicKey(publicKey).export({format: 'der', type: 'spki'})).digest('base64');
  const credentials = {email: `browser-${crypto.randomBytes(8).toString('hex')}@example.test`, password: crypto.randomBytes(24).toString('hex')};
  redactions.push(credentials.email, credentials.password);
  const created = await new Promise((resolve, reject) => {
    const request = https.request(origin + '/api/v1/auth/register', {method: 'POST', ca, headers: {'Content-Type': 'application/json'}}, response => {
      response.resume();
      response.on('end', () => resolve(response.statusCode));
    });
    request.on('error', reject);
    request.end(JSON.stringify(credentials));
  });
  assert.equal(created, 201);
  // Trust only this fixture's public key in the isolated browser process, never all HTTPS errors.
  const browser = await chromium.launch({executablePath: process.env.CHROME_BIN || undefined, headless: true,
    args: ['--no-sandbox', '--ignore-certificate-errors-spki-list=' + pin]});
  let page;
  const statuses = [];
  try {
    const context = await browser.newContext({viewport: {width: 390, height: 844}, isMobile: true, hasTouch: true});
    page = await context.newPage();
    page.on('response', response => {
      const path = new URL(response.url()).pathname;
      if (path.startsWith('/api/')) statuses.push({path, status: response.status()});
    });
    const errors = [];
    page.on('pageerror', error => errors.push(error.name));
    const activeValue = expected => {
      let active = document.activeElement;
      while (active && active.shadowRoot && active.shadowRoot.activeElement) active = active.shadowRoot.activeElement;
      return active && active.value === expected;
    };
    const enterText = async (label, value) => {
      await page.getByRole('textbox', {name: label, exact: true}).click();
      await page.keyboard.press('Control+A');
      await page.keyboard.insertText(value);
      await page.waitForFunction(activeValue, value);
    };
    await page.goto(origin, {waitUntil: 'networkidle'});
    // Flutter builds semantic DOM on accessibility activation.
    await page.locator('flt-semantics-placeholder').evaluate(node => node.click());
    await page.getByRole('button', {name: 'Entrar', exact: true}).click();
    // Flutter paints route transitions on canvas while semantic proxy bounds may already be final.
    await page.waitForTimeout(400);
    await enterText('E-mail', credentials.email);
    await enterText('Senha', credentials.password);
    await page.getByRole('button', {name: 'Entrar', exact: true}).click();
    await page.getByRole('button', {name: 'Meus registros', exact: true}).click();
    await page.getByText('Seu trono ainda não tem histórias.', {exact: true}).waitFor();
    await page.getByRole('button', {name: 'Registrar minha cagada', exact: true}).click();
    await page.waitForTimeout(400);
    await enterText('Conte sua história (opcional)', 'Meu primeiro registro no Docker');
    await page.getByRole('button', {name: 'Salvar registro privado', exact: true}).click();
    await page.getByRole('button', {name: 'Editar registro', exact: true}).click();
    await page.waitForTimeout(400);
    // Flutter's semantic inputs are proxies. Focus activates the real editing element with its value.
    await page.getByRole('textbox', {name: 'Conte sua história (opcional)', exact: true}).click();
    await page.waitForFunction(activeValue, 'Meu primeiro registro no Docker');
    await page.getByRole('button', {name: 'Voltar ao histórico', exact: true}).click();
    await page.getByRole('button', {name: 'Editar registro', exact: true}).waitFor();
    if (process.env.TRONO_PREVIEW_SCREENSHOT) await page.screenshot({path: process.env.TRONO_PREVIEW_SCREENSHOT});
    await page.reload({waitUntil: 'networkidle'});
    await page.locator('flt-semantics-placeholder').evaluate(node => node.click());
    await page.getByRole('button', {name: 'Entrar', exact: true}).waitFor();
    assert.equal(await page.getByRole('button', {name: 'Editar registro', exact: true}).count(), 0);
    const saved = await page.evaluate(() => ({local: Object.keys(localStorage), session: Object.keys(sessionStorage)}));
    assert.equal(saved.local.some(key => /trono|refresh|flutter_secure_storage/i.test(key)), false);
    assert.equal(saved.session.some(key => /trono|refresh|flutter_secure_storage/i.test(key)), false);
    assert.deepEqual(errors, []);
    console.log('Navegador 390×844: login, registro privado e recarga sem credenciais persistidas: OK');
    await context.close();
  } catch (error) {
    if (page) {
      if (process.env.TRONO_PREVIEW_SCREENSHOT) await page.screenshot({path: process.env.TRONO_PREVIEW_SCREENSHOT + '.failure.png'});
      const text = (await page.locator('flt-semantics').allTextContents()).join(' ').slice(0, 2000);
      error.message += '\nAPI statuses: ' + JSON.stringify(statuses) + '\nInterface: ' + text;
    }
    throw error;
  } finally {
    await browser.close();
  }
})().catch(error => {
  // Never print browser requests, responses, credentials or access tokens.
  let message = error.name + ': ' + error.message;
  for (const secret of redactions) message = message.split(secret).join('[redacted]');
  console.error(message);
  process.exitCode = 1;
});
