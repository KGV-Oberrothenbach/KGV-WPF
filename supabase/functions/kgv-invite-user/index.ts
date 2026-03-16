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
): Promise<string | null> {
  const normalized = (email ?? "").trim().toLowerCase();
  if (!normalized) return null;

  // No provider assumptions: lookup is purely email-based.
  // listUsers is the only reliable admin API we can use without direct auth schema queries.
  const perPage = 200;
  for (let page = 1; page <= 10; page++) {
    const { data, error } = await supabaseAdmin.auth.admin.listUsers({ page, perPage });
    if (error) {
      console.log("[kgv-invite-user] listUsers failed", { page, message: error.message });
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
  try {
    if (req.method !== "POST") {
      return json(
        { success: false, outcome: "error", errorCode: "MethodNotAllowed", message: "Nur POST ist erlaubt." },
        405,
      );
    }

    const authHeader = req.headers.get("Authorization");
    if (!authHeader) {
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
      return json(
        { success: false, outcome: "unauthorized", errorCode: "Unauthorized", message: "Keine gültige Session." },
        401,
      );
    }

    const callerUserId = userData.user.id;

    const { data: callerAppUser, error: callerAppUserError } = await supabaseAdmin
      .from("app_user")
      .select("role, mitglied_id")
      .eq("user_id", callerUserId)
      .maybeSingle();

    if (callerAppUserError) {
      console.log("[kgv-invite-user] caller app_user lookup failed", { message: callerAppUserError.message });
      return json(
        { success: false, outcome: "error", errorCode: "ServerError", message: "Serverfehler." },
        500,
      );
    }

    const callerRole = (callerAppUser?.role ?? "").trim().toLowerCase();
    if (callerRole !== "admin" && callerRole !== "vorstand") {
      return json(
        { success: false, outcome: "unauthorized", errorCode: "Unauthorized", message: "Keine Berechtigung." },
        403,
      );
    }

    const body = (await req.json()) as InviteRequest;
    const mitgliedId = Number(body?.mitgliedId);
    const role = String(body?.role ?? "").trim().toLowerCase();

    if (!Number.isInteger(mitgliedId) || mitgliedId <= 0) {
      return json(
        { success: false, outcome: "error", errorCode: "InvalidMitgliedId", message: "Ungültige Mitglied-ID." },
        400,
      );
    }

    if (!allowedRoles.has(role)) {
      return json(
        { success: false, outcome: "invalid_role", errorCode: "InvalidRole", message: "Ungültige Rolle." },
        400,
      );
    }

    const inviteMethod = String((body as any)?.inviteMethod ?? "otp").trim().toLowerCase();
    if (inviteMethod && inviteMethod !== "otp") {
      // Admin-Invite ist OTP-only. Kein Fallback auf OAuth/Provider-Logik.
      console.log("[kgv-invite-user] Non-OTP inviteMethod requested; forcing otp", { inviteMethod, mitgliedId });
    }

    const { data: mitglied, error: mitgliedError } = await supabaseAdmin
      .from("mitglied")
      .select("id, vorname, name, email, auth_user_id, role")
      .eq("id", mitgliedId)
      .maybeSingle();

    if (mitgliedError) {
      console.log("[kgv-invite-user] mitglied lookup failed", { mitgliedId, message: mitgliedError.message });
      return json(
        { success: false, outcome: "error", errorCode: "ServerError", message: "Serverfehler." },
        500,
      );
    }

    if (!mitglied) {
      return json(
        { success: false, outcome: "not_found", errorCode: "NotFound", message: "Mitglied nicht gefunden." },
        404,
      );
    }

    const email = String(mitglied.email ?? "").trim();
    if (!email) {
      return json(
        { success: false, outcome: "missing_email", errorCode: "MissingEmail", message: "Keine E-Mail-Adresse vorhanden." },
        400,
      );
    }

    if (mitglied.auth_user_id) {
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
      console.log("[kgv-invite-user] existing app_user lookup failed", { mitgliedId, message: existingAppUserError.message });
      return json(
        { success: false, outcome: "error", errorCode: "ServerError", message: "Serverfehler." },
        500,
      );
    }

    if (existingAppUser) {
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
      console.log("[kgv-invite-user] mitglied role update failed", { mitgliedId, message: updateMitgliedRoleError.message });
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

    console.log("[kgv-invite-user] OTP invite start", { mitgliedId, email, inviteRedirectTo: !!inviteRedirectTo });

    const { error: otpError } = await otpClient.auth.signInWithOtp({
      email,
      options: otpOptions,
    });

    if (otpError) {
      console.log("[kgv-invite-user] OTP invite failed", { mitgliedId, email, message: otpError.message });
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
    const userId = await tryFindAuthUserIdByEmail(supabaseAdmin, email);
    if (!userId) {
      console.log("[kgv-invite-user] OTP sent but userId could not be resolved", { mitgliedId, email });
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

    const { error: linkMitgliedError } = await supabaseAdmin
      .from("mitglied")
      .update({ auth_user_id: userId, role })
      .eq("id", mitgliedId);

    if (linkMitgliedError) {
      console.log("[kgv-invite-user] mitglied link failed", { mitgliedId, userId, message: linkMitgliedError.message });
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
      console.log("[kgv-invite-user] app_user upsert failed", { mitgliedId, userId, message: upsertAppUserError.message });
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
    console.log("[kgv-invite-user] UnhandledError", { message });
    return json(
      { success: false, outcome: "error", errorCode: "UnhandledError", message: "Serverfehler." },
      500,
    );
  }
});