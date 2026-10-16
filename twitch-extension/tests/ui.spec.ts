import { test, expect } from "@playwright/test";
for (const [view, width, height] of [
  ["panel", 320, 500],
  ["mobile", 390, 844],
] as const) {
  test(`${view}: navigation, actions, countdown and isolation`, async ({
    page,
  }) => {
    await page.setViewportSize({ width, height });
    const errors: string[] = [];
    page.on("pageerror", (e) => errors.push(e.message));
    const requests: string[] = [];
    page.on("request", (r) => requests.push(r.url()));
    // Deterministic helper stub: no real Twitch session or account is contacted by tests.
    await page.route("https://extension-files.twitch.tv/**", (r) =>
      r.fulfill({
        contentType: "text/javascript",
        body: "window.Twitch={ext:{onAuthorized:cb=>cb()}}",
      }),
    );
    await page.goto(`/${view}.html`);
    await expect(
      page.getByRole("heading", { name: "Charakter", exact: true }),
    ).toBeVisible();
    for (const name of [
      "Abenteuer",
      "Inventar",
      "Ausrüstung",
      "Itemsets",
      "Perks",
      "Boss",
      "Hot Time",
      "Charakter",
    ]) {
      await page.getByRole("button", { name, exact: true }).click();
      await expect(
        page.getByRole("heading", { name, exact: true }),
      ).toBeVisible();
      expect(
        await page.evaluate(
          () => document.documentElement.scrollWidth <= innerWidth,
        ),
      ).toBe(true);
    }
    await page.evaluate(() => scrollTo(0, 0));
    await page.screenshot({ path: `test-results/character-${view}.png` });
    await page.getByLabel("Animationen aus").check();
    await page.getByRole("button", { name: "Abenteuer", exact: true }).click();
    await page.getByRole("button", { name: "Tauchgang starten" }).click();
    await expect(
      page.getByText("+300 Demo-Perlen", { exact: false }),
    ).toBeVisible();
    await expect(
      page.getByRole("button", { name: /Tauchen ·/ }),
    ).toBeDisabled();
    await page.getByRole("button", { name: "Angel auswerfen" }).click();
    await expect(page.getByText("Ein schimmernder Fang!")).toBeVisible();
    await expect(page.getByText("Kristallkarpfen · Rare")).toBeVisible();
    await page.getByRole("button", { name: "Inventar", exact: true }).click();
    const shell = page.locator('[data-instance-id="demo-shell"]');
    await shell.getByRole("button", { name: "Favorisieren" }).click();
    await expect(
      shell.getByRole("button", { name: /Zerlegen/ }),
    ).toBeDisabled();
    await shell.getByRole("button", { name: "Favorisiert" }).click();
    await shell.getByRole("button", { name: /Zerlegen/ }).click();
    await expect(shell).toHaveCount(0);
    const amulet = page
      .locator("article")
      .filter({
        has: page.getByRole("heading", { name: "Amulett der Gezeiten" }),
      })
      .first();
    await amulet
      .getByRole("button", { name: "Ausrüsten", exact: true })
      .click();
    await expect(amulet.getByRole("button", { name: "Ablegen" })).toBeVisible();
    await amulet.getByRole("button", { name: "Ablegen" }).click();
    await page.getByRole("button", { name: "Perks", exact: true }).click();
    await page
      .getByRole("button", { name: "Kaufen · 500 Demo-Perlen" })
      .first()
      .click();
    await expect(page.getByText("1/5")).toBeVisible();
    await page.getByRole("button", { name: "Boss", exact: true }).click();
    await page.getByRole("button", { name: /Teilnehmen/ }).click();
    await expect(
      page.getByText("Dein Schaden: 0", { exact: true }),
    ).toHaveCount(0);
    await page.getByRole("button", { name: "Hot Time", exact: true }).click();
    await page.getByRole("button", { name: "Letzte 30 Sekunden" }).click();
    await expect(page.locator(".countdown")).toHaveText("0:30");
    await expect(page.locator(".countdown")).toHaveText("0:29", {
      timeout: 3000,
    });
    await page.getByRole("button", { name: "Beenden", exact: true }).click();
    await expect(page.locator(".countdown")).toHaveText("0:00");
    await page.evaluate(() => scrollTo(0, 0));
    await page.screenshot({ path: `test-results/${view}.png`, fullPage: true });
    await page.reload();
    await expect(
      page.getByText("25.000 Demo-Perlen", { exact: false }),
    ).toBeVisible();
    expect(errors).toEqual([]);
    expect(
      requests.every(
        (url) =>
          url.startsWith("https://localhost:8080/") ||
          url.startsWith("https://extension-files.twitch.tv/"),
      ),
    ).toBe(true);
  });
}
test("configuration and helper as first script, reduced motion", async ({
  page,
  request,
}) => {
  await page.route("https://extension-files.twitch.tv/**", (r) =>
    r.fulfill({ contentType: "text/javascript", body: "" }),
  );
  await page.emulateMedia({ reducedMotion: "reduce" });
  await page.goto("/config.html");
  await expect(
    page.getByRole("heading", { name: "Demo-Konfiguration" }),
  ).toBeVisible();
  await page.getByRole("button", { name: "5 Minuten starten" }).click();
  expect(
    await page
      .locator(".burning")
      .evaluate((e) => getComputedStyle(e).animationName),
  ).toBe("none");
  for (const view of ["panel", "mobile", "config"]) {
    const response = await request.get(`/${view}.html`);
    expect(response.status()).toBe(200);
    expect((await response.text()).match(/<script[^>]*src="([^"]+)/)?.[1]).toBe(
      "https://extension-files.twitch.tv/helper/v1/twitch-ext.min.js",
    );
    expect(response.headers()["content-security-policy"]).toContain(
      "connect-src 'none'",
    );
  }
});
