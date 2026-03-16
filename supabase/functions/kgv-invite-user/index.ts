// @ts-nocheck
import { createClient } from "npm:@supabase/supabase-js@2";

type InviteRequest = {
  mitgliedId: number;
  role: string;
  inviteMethod?: string;
};

type JsonResponse = {
  success: boolean;
  outcome?: string;
  errorCode?: string;
  message: string;
  mitgliedId?: number;
  email?: string;
  userId?: string;
  authUserId?: string;
};

const allowedRoles = new Set(["admin", "vorstand", "user"]);

function newRequestId() {
  try {
    return crypto.randomUUID();
  } catch {
    return String(Date.now());
  }
}

function maskEmail(email?: string) {
  const e = String(email ?? "").trim();
  const at = e.indexOf("@");
  if (at <= 1) return e ? "***" : "";
  const user = e.slice(0, at);
  const domain = e.slice(at + 1);
  const prefix = user.slice(0, 2);
  return `${prefix}***@${domain}`;
}

function classifyAuthError(message?: string) {
  const m = String(message ?? "").toLowerCase();
  if (!m) return "Unknown";
  if (m.includes("not enabled for this sign-in method")) return "SignInMethodConflict";
  if (m.includes("provider")) return "ProviderConflict";
  if (m.includes("smtp") || m.includes("email") && m.includes("send")) return "MailSendFailed";
  if (m.includes("config") || m.includes("missing") && m.includes("key")) return "ConfigError";
  if (m.includes("rate") && m.includes("limit")) return "RateLimited";
  return "Other";
}

function safeErrorInfo(err: any) {
  if (!err) return null;
  return {
    name: err?.name,
    status: err?.status,
    code: err?.code,
    message: err?.message,
  };
}

function json(body: JsonResponse, status = 200) {
  return new Response(JSON.stringify(body), {
    status,
    headers: {
      "Content-Type": "application/json",
    },
  });
}

async function tryFindAuthUserIdByEmail(
  supabaseAdmin: any,
  email: string,
  requestId?: string,
): Promise<string | null> {
  const normalized = (email ?? "").trim().toLowerCase();
  if (!normalized) return null;

  // No provider assumptions: lookup is purely email-based.
  // listUsers is the only reliable admin API we can use without direct auth schema queries.
  const perPage = 200;
  for (let page = 1; page <= 10; page++) {
    const { data, error } = await supabaseAdmin.auth.admin.listUsers({ page, perPage });
    if (error) {
      console.log("[kgv-invite-user] listUsers failed", { requestId, page, error: safeErrorInfo(error) });
      return null;
    }

    const users = data?.users ?? [];
    const hit = users.find((u: any) => String(u?.email ?? "").trim().toLowerCase() === normalized);
    if (hit?.id) return hit.id;

    if (users.length < perPage) break;
  }

  return null;
}

Deno.serve(async (req: Request) => {
  const requestId = newRequestId();
  try {
    const clientRequestId = req.headers.get("x-kgv-client-request-id") ?? undefined;
    console.log("[kgv-invite-user] request start", { requestId, clientRequestId, method: req.method, url: req.url });

    if (req.method !== "POST") {
      console.log("[kgv-invite-user] reject: method", { requestId, method: req.method });
      return json(
        { success: false, outcome: "error", errorCode: "MethodNotAllowed", message: "Nur POST ist erlaubt." },
        405,
      );
    }

    const authHeader = req.headers.get("Authorization");
    const isBearer = !!authHeader && authHeader.trim().toLowerCase().startsWith("bearer ");
    const bearerTokenLength = isBearer ? authHeader.trim().length - "Bearer ".length : 0;
    console.log("[kgv-invite-user] auth header", { requestId, clientRequestId, hasAuthorizationHeader: !!authHeader, isBearer, bearerTokenLength });
    if (!authHeader) {
      console.log("[kgv-invite-user] reject: missing auth header", { requestId });
      return json(
        { success: false, outcome: "unauthorized", errorCode: "Unauthorized", message: "Keine Berechtigung." },
        401,
      );
    }

    const supabaseUrl = Deno.env.get("SUPABASE_URL") ?? "";
    const supabaseAnonKey = Deno.env.get("SUPABASE_ANON_KEY") ?? "";
    const serviceRoleKey = Deno.env.get("SUPABASE_SERVICE_ROLE_KEY") ?? "";
    const inviteRedirectTo = Deno.env.get("INVITE_REDIRECT_TO") ?? undefined;

    if (!supabaseUrl || !supabaseAnonKey || !serviceRoleKey) {
      console.log("[kgv-invite-user] reject: server config missing", {
        requestId,
        hasUrl: !!supabaseUrl,
        hasAnon: !!supabaseAnonKey,
        hasService: !!serviceRoleKey,
      });
      return json(
        { success: false, outcome: "error", errorCode: "ServerConfig", message: "Server-Konfiguration unvollständig." },
        500,
      );
    }

    const supabaseUser = createClient(supabaseUrl, supabaseAnonKey, {
      global: {
        headers: {
          Authorization: authHeader,
        },
      },
    });

    const supabaseAdmin = createClient(supabaseUrl, serviceRoleKey);

    const { data: userData, error: userError } = await supabaseUser.auth.getUser();
    if (userError || !userData.user) {
      console.log("[kgv-invite-user] reject: invalid session", { requestId, clientRequestId, error: safeErrorInfo(userError) });
      return json(
        { success: false, outcome: "unauthorized", errorCode: "Unauthorized", message: "Keine gültige Session." },
        401,
      );
    }

    const callerUserId = userData.user.id;
    console.log("[kgv-invite-user] caller", { requestId, clientRequestId, callerUserId });

    const { data: callerAppUser, error: callerAppUserError } = await supabaseAdmin
      .from("app_user")
      .select("role, mitglied_id")
      .eq("user_id", callerUserId)
      .maybeSingle();

    if (callerAppUserError) {
      console.log("[kgv-invite-user] caller app_user lookup failed", { requestId, error: safeErrorInfo(callerAppUserError) });
      return json(
        { success: false, outcome: "error", errorCode: "ServerError", message: "Serverfehler." },
        500,
      );
    }

    const callerRole = (callerAppUser?.role ?? "").trim().toLowerCase();
    if (callerRole !== "admin" && callerRole !== "vorstand") {
      console.log("[kgv-invite-user] reject: insufficient role", { requestId, callerRole });
      return json(
        { success: false, outcome: "unauthorized", errorCode: "Unauthorized", message: "Keine Berechtigung." },
        403,
      );
    }

    const body = (await req.json()) as InviteRequest;
    const mitgliedId = Number(body?.mitgliedId);
    const role = String(body?.role ?? "").trim().toLowerCase();
    const inviteMethod = String((body as any)?.inviteMethod ?? "otp").trim().toLowerCase();

    console.log("[kgv-invite-user] payload", {
      requestId,
      mitgliedId,
      role,
      inviteMethod,
    });

    if (!Number.isInteger(mitgliedId) || mitgliedId <= 0) {
      console.log("[kgv-invite-user] reject: invalid mitgliedId", { requestId, mitgliedId });
      return json(
        { success: false, outcome: "error", errorCode: "InvalidMitgliedId", message: "Ungültige Mitglied-ID." },
        400,
      );
    }

    if (!allowedRoles.has(role)) {
      console.log("[kgv-invite-user] reject: invalid role", { requestId, role });
      return json(
        { success: false, outcome: "invalid_role", errorCode: "InvalidRole", message: "Ungültige Rolle." },
        400,
      );
    }
    if (inviteMethod && inviteMethod !== "otp") {
      // Admin-Invite ist OTP-only. Kein Fallback auf OAuth/Provider-Logik.
      console.log("[kgv-invite-user] Non-OTP inviteMethod requested; forcing otp", { requestId, inviteMethod, mitgliedId });
    }

    const { data: mitglied, error: mitgliedError } = await supabaseAdmin
      .from("mitglied")
      .select("id, vorname, name, email, auth_user_id, role")
      .eq("id", mitgliedId)
      .maybeSingle();

    if (mitgliedError) {
      console.log("[kgv-invite-user] mitglied lookup failed", { requestId, mitgliedId, error: safeErrorInfo(mitgliedError) });
      return json(
        { success: false, outcome: "error", errorCode: "ServerError", message: "Serverfehler." },
        500,
      );
    }

    if (!mitglied) {
      console.log("[kgv-invite-user] reject: mitglied not found", { requestId, mitgliedId });
      return json(
        { success: false, outcome: "not_found", errorCode: "NotFound", message: "Mitglied nicht gefunden." },
        404,
      );
    }

    const email = String(mitglied.email ?? "").trim();
    if (!email) {
      console.log("[kgv-invite-user] reject: missing email", { requestId, mitgliedId });
      return json(
        { success: false, outcome: "missing_email", errorCode: "MissingEmail", message: "Keine E-Mail-Adresse vorhanden." },
        400,
      );
    }

    if (mitglied.auth_user_id) {
      console.log("[kgv-invite-user] reject: already linked", { requestId, mitgliedId, email: maskEmail(email) });
      return json(
        {
          success: false,
          outcome: "already_linked",
          errorCode: "AlreadyLinked",
          message: "Für dieses Mitglied existiert bereits ein verknüpfter Account.",
          mitgliedId,
          email,
        },
        409,
      );
    }

    const { data: existingAppUser, error: existingAppUserError } = await supabaseAdmin
      .from("app_user")
      .select("user_id, mitglied_id, role")
      .eq("mitglied_id", mitgliedId)
      .maybeSingle();

    if (existingAppUserError) {
      console.log("[kgv-invite-user] existing app_user lookup failed", { requestId, mitgliedId, error: safeErrorInfo(existingAppUserError) });
      return json(
        { success: false, outcome: "error", errorCode: "ServerError", message: "Serverfehler." },
        500,
      );
    }

    if (existingAppUser) {
      console.log("[kgv-invite-user] reject: app_user exists", { requestId, mitgliedId, email: maskEmail(email) });
      return json(
        {
          success: false,
          outcome: "user_already_exists",
          errorCode: "UserAlreadyExists",
          message: "Für dieses Mitglied existiert bereits ein Nutzerkonto.",
          mitgliedId,
          email,
        },
        409,
      );
    }

    const { error: updateMitgliedRoleError } = await supabaseAdmin
      .from("mitglied")
      .update({ role })
      .eq("id", mitgliedId);

    if (updateMitgliedRoleError) {
      console.log("[kgv-invite-user] mitglied role update failed", { requestId, mitgliedId, error: safeErrorInfo(updateMitgliedRoleError) });
      return json(
        { success: false, outcome: "error", errorCode: "ServerError", message: "Serverfehler." },
        500,
      );
    }

    // OTP-only Admin-Invite (keine Provider-Prüfung / kein OAuth / kein Invite-by-Password)
    const otpClient = createClient(supabaseUrl, supabaseAnonKey);
    const otpOptions: Record<string, unknown> = {
      shouldCreateUser: true,
      data: {
        vorname: mitglied.vorname,
        name: mitglied.name,
      },
    };

    if (inviteRedirectTo) {
      otpOptions.emailRedirectTo = inviteRedirectTo;
    }

    console.log("[kgv-invite-user] OTP invite start", {
      requestId,
      mitgliedId,
      email: maskEmail(email),
      inviteRedirectTo: !!inviteRedirectTo,
      authCall: "signInWithOtp",
    });

    const { error: otpError } = await otpClient.auth.signInWithOtp({
      email,
      options: otpOptions,
    });

    if (otpError) {
      const classification = classifyAuthError(otpError.message);
      console.log("[kgv-invite-user] OTP invite failed", {
        requestId,
        mitgliedId,
        email: maskEmail(email),
        classification,
        error: safeErrorInfo(otpError),
      });
      return json(
        {
          success: false,
          outcome: "error",
          errorCode: "InviteFailed",
          message: "Das Nutzerkonto konnte nicht per OTP eingeladen werden.",
          mitgliedId,
          email,
        },
        400,
      );
    }

    // After OTP send: resolve/create the auth user id (needed for membership linking).
    const userId = await tryFindAuthUserIdByEmail(supabaseAdmin, email, requestId);
    if (!userId) {
      console.log("[kgv-invite-user] OTP sent but userId could not be resolved", { requestId, mitgliedId, email: maskEmail(email) });
      return json(
        {
          success: false,
          outcome: "error",
          errorCode: "MissingUserId",
          message: "OTP wurde versendet, aber der Nutzer konnte nicht aufgelöst werden.",
          mitgliedId,
          email,
        },
        500,
      );
    }

    console.log("[kgv-invite-user] auth user resolved", { requestId, mitgliedId, userId });

    const { error: linkMitgliedError } = await supabaseAdmin
      .from("mitglied")
      .update({ auth_user_id: userId, role })
      .eq("id", mitgliedId);

    if (linkMitgliedError) {
      console.log("[kgv-invite-user] mitglied link failed", { requestId, mitgliedId, userId, error: safeErrorInfo(linkMitgliedError) });
      return json(
        { success: false, outcome: "error", errorCode: "LinkFailed", message: "Verknüpfung mit Mitglied fehlgeschlagen.", mitgliedId, email, userId },
        500,
      );
    }

    const { error: upsertAppUserError } = await supabaseAdmin
      .from("app_user")
      .upsert(
        {
          user_id: userId,
          mitglied_id: mitgliedId,
          role,
          updated_at: new Date().toISOString(),
        },
        { onConflict: "user_id" },
      );

    if (upsertAppUserError) {
      console.log("[kgv-invite-user] app_user upsert failed", { requestId, mitgliedId, userId, error: safeErrorInfo(upsertAppUserError) });
      return json(
        { success: false, outcome: "error", errorCode: "AppUserUpsertFailed", message: "Nutzerkonto konnte nicht gespeichert werden.", mitgliedId, email, userId },
        500,
      );
    }

    return json({
      success: true,
      outcome: "invited",
      message: "OTP wurde versendet.",
      mitgliedId,
      email,
      userId,
      authUserId: userId,
    });
  } catch (err) {
    const message = err instanceof Error ? err.message : "Unknown error";
    console.log("[kgv-invite-user] UnhandledError", { requestId, message });
    return json(
      { success: false, outcome: "error", errorCode: "UnhandledError", message: "Serverfehler." },
      500,
    );
  }
});