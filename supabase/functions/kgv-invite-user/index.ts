// @ts-nocheck
import { createClient } from "npm:@supabase/supabase-js@2";

type InviteRequest = {
  mitgliedId: number;
  role: string;
};

type JsonResponse = {
  success: boolean;
  errorCode?: string;
  message: string;
  mitgliedId?: number;
  email?: string;
  userId?: string;
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

Deno.serve(async (req: Request) => {
  try {
    if (req.method !== "POST") {
      return json(
        { success: false, errorCode: "MethodNotAllowed", message: "Only POST is allowed." },
        405,
      );
    }

    const authHeader = req.headers.get("Authorization");
    if (!authHeader) {
      return json(
        { success: false, errorCode: "Unauthorized", message: "Missing Authorization header." },
        401,
      );
    }

    const supabaseUrl = Deno.env.get("SUPABASE_URL") ?? "";
    const supabaseAnonKey = Deno.env.get("SUPABASE_ANON_KEY") ?? "";
    const serviceRoleKey = Deno.env.get("SUPABASE_SERVICE_ROLE_KEY") ?? "";
    const inviteRedirectTo = Deno.env.get("INVITE_REDIRECT_TO") ?? undefined;

    if (!supabaseUrl || !supabaseAnonKey || !serviceRoleKey) {
      return json(
        { success: false, errorCode: "ServerConfig", message: "Missing required environment variables." },
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
        { success: false, errorCode: "Unauthorized", message: "Invalid user session." },
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
      return json(
        { success: false, errorCode: "ServerError", message: callerAppUserError.message },
        500,
      );
    }

    const callerRole = (callerAppUser?.role ?? "").trim().toLowerCase();
    if (callerRole !== "admin" && callerRole !== "vorstand") {
      return json(
        { success: false, errorCode: "Unauthorized", message: "Keine Berechtigung." },
        403,
      );
    }

    const body = (await req.json()) as InviteRequest;
    const mitgliedId = Number(body?.mitgliedId);
    const role = String(body?.role ?? "").trim().toLowerCase();

    if (!Number.isInteger(mitgliedId) || mitgliedId <= 0) {
      return json(
        { success: false, errorCode: "InvalidMitgliedId", message: "Ungültige Mitglied-ID." },
        400,
      );
    }

    if (!allowedRoles.has(role)) {
      return json(
        { success: false, errorCode: "InvalidRole", message: "Ungültige Rolle." },
        400,
      );
    }

    const { data: mitglied, error: mitgliedError } = await supabaseAdmin
      .from("mitglied")
      .select("id, vorname, name, email, auth_user_id, role")
      .eq("id", mitgliedId)
      .maybeSingle();

    if (mitgliedError) {
      return json(
        { success: false, errorCode: "ServerError", message: mitgliedError.message },
        500,
      );
    }

    if (!mitglied) {
      return json(
        { success: false, errorCode: "NotFound", message: "Mitglied nicht gefunden." },
        404,
      );
    }

    const email = String(mitglied.email ?? "").trim();
    if (!email) {
      return json(
        { success: false, errorCode: "MissingEmail", message: "Keine E-Mail-Adresse vorhanden." },
        400,
      );
    }

    if (mitglied.auth_user_id) {
      return json(
        {
          success: false,
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
      return json(
        { success: false, errorCode: "ServerError", message: existingAppUserError.message },
        500,
      );
    }

    if (existingAppUser) {
      return json(
        {
          success: false,
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
      return json(
        { success: false, errorCode: "ServerError", message: updateMitgliedRoleError.message },
        500,
      );
    }

    const inviteOptions: Record<string, unknown> = {
      data: {
        vorname: mitglied.vorname,
        name: mitglied.name,
      },
    };

    if (inviteRedirectTo) {
      inviteOptions.redirectTo = inviteRedirectTo;
    }

    const { data: inviteData, error: inviteError } = await supabaseAdmin.auth.admin.inviteUserByEmail(
      email,
      inviteOptions,
    );

    if (inviteError) {
      return json(
        { success: false, errorCode: "InviteFailed", message: inviteError.message, mitgliedId, email },
        400,
      );
    }

    const userId = inviteData.user?.id;
    if (!userId) {
      return json(
        {
          success: false,
          errorCode: "MissingUserId",
          message: "Invite wurde gesendet, aber es wurde keine User-ID zurückgegeben.",
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
      return json(
        { success: false, errorCode: "LinkFailed", message: linkMitgliedError.message, mitgliedId, email, userId },
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
      return json(
        { success: false, errorCode: "AppUserUpsertFailed", message: upsertAppUserError.message, mitgliedId, email, userId },
        500,
      );
    }

    return json({
      success: true,
      message: "Einladungs-Mail wurde versendet.",
      mitgliedId,
      email,
      userId,
    });
  } catch (err) {
    const message = err instanceof Error ? err.message : "Unknown error";
    return json(
      { success: false, errorCode: "UnhandledError", message },
      500,
    );
  }
});