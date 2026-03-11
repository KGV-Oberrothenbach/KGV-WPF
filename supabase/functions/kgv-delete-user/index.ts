import "jsr:@supabase/functions-js/edge-runtime.d.ts";
import { createClient } from "https://esm.sh/@supabase/supabase-js@2";

type DeleteRequest = {
  mitgliedId?: number;
};

type JsonResponse = {
  outcome: "deleted" | "no_user" | "forbidden" | "bad_request" | "not_found" | "error";
  message: string;
  mitgliedId?: number;
  authUserId?: string;
};

const corsHeaders = {
  "Access-Control-Allow-Origin": "*",
  "Access-Control-Allow-Headers": "authorization, x-client-info, apikey, content-type",
  "Access-Control-Allow-Methods": "POST, OPTIONS",
  "Content-Type": "application/json; charset=utf-8",
};

function json(body: JsonResponse, status = 200): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: corsHeaders,
  });
}

Deno.serve(async (req: Request) => {
  if (req.method === "OPTIONS") {
    return new Response("ok", { headers: corsHeaders });
  }

  if (req.method !== "POST") {
    return json(
      {
        outcome: "bad_request",
        message: "Only POST is allowed.",
      },
      405,
    );
  }

  try {
    const supabaseUrl = Deno.env.get("SUPABASE_URL");
    const anonKey = Deno.env.get("SUPABASE_ANON_KEY");
    const serviceRoleKey =
      Deno.env.get("SUPABASE_SERVICE_ROLE_KEY") ?? Deno.env.get("SERVICE_ROLE_KEY");

    if (!supabaseUrl || !anonKey || !serviceRoleKey) {
      return json(
        {
          outcome: "error",
          message: "Missing Supabase environment configuration.",
        },
        500,
      );
    }

    const authHeader = req.headers.get("Authorization");
    if (!authHeader?.startsWith("Bearer ")) {
      return json(
        {
          outcome: "forbidden",
          message: "Missing or invalid Authorization header.",
        },
        401,
      );
    }

    const requestBody = (await req.json()) as DeleteRequest;
    const mitgliedId = Number(requestBody?.mitgliedId);

    if (!Number.isInteger(mitgliedId) || mitgliedId <= 0) {
      return json(
        {
          outcome: "bad_request",
          message: "mitgliedId must be a positive integer.",
        },
        400,
      );
    }

    const callerClient = createClient(supabaseUrl, anonKey, {
      global: {
        headers: {
          Authorization: authHeader,
        },
      },
      auth: {
        persistSession: false,
        autoRefreshToken: false,
      },
    });

    const adminClient = createClient(supabaseUrl, serviceRoleKey, {
      auth: {
        persistSession: false,
        autoRefreshToken: false,
      },
    });

    const { data: callerUserData, error: callerUserError } = await callerClient.auth.getUser();
    if (callerUserError || !callerUserData.user) {
      return json(
        {
          outcome: "forbidden",
          message: "User could not be authenticated.",
          mitgliedId,
        },
        401,
      );
    }

    const { data: roleData, error: roleError } = await callerClient.rpc("get_user_role");
    if (roleError) {
      return json(
        {
          outcome: "forbidden",
          message: `Role check failed: ${roleError.message}`,
          mitgliedId,
        },
        403,
      );
    }

    const callerRole = String(roleData ?? "").trim().toLowerCase();
    const isAllowed = callerRole === "admin" || callerRole === "vorstand";

    if (!isAllowed) {
      return json(
        {
          outcome: "forbidden",
          message: "Only admin or vorstand may delete linked user accounts.",
          mitgliedId,
        },
        403,
      );
    }

    const { data: mitgliedRow, error: mitgliedReadError } = await adminClient
      .from("mitglied")
      .select("id, auth_user_id")
      .eq("id", mitgliedId)
      .maybeSingle();

    if (mitgliedReadError) {
      return json(
        {
          outcome: "error",
          message: `Mitglied lookup failed: ${mitgliedReadError.message}`,
          mitgliedId,
        },
        500,
      );
    }

    if (!mitgliedRow) {
      return json(
        {
          outcome: "not_found",
          message: "Mitglied not found.",
          mitgliedId,
        },
        404,
      );
    }

    const authUserId = mitgliedRow.auth_user_id as string | null;

    if (!authUserId) {
      return json(
        {
          outcome: "no_user",
          message: "For this Mitglied no linked auth user exists.",
          mitgliedId,
        },
        200,
      );
    }

    const { error: unlinkError } = await adminClient
      .from("mitglied")
      .update({ auth_user_id: null })
      .eq("id", mitgliedId);

    if (unlinkError) {
      return json(
        {
          outcome: "error",
          message: `Unlinking auth_user_id failed: ${unlinkError.message}`,
          mitgliedId,
          authUserId,
        },
        500,
      );
    }

    const { error: deleteAuthError } = await adminClient.auth.admin.deleteUser(authUserId);

    if (deleteAuthError) {
      return json(
        {
          outcome: "error",
          message: `Deleting auth user failed: ${deleteAuthError.message}`,
          mitgliedId,
          authUserId,
        },
        500,
      );
    }

    return json(
      {
        outcome: "deleted",
        message: "Nutzerkonto wurde erfolgreich gelöscht.",
        mitgliedId,
        authUserId,
      },
      200,
    );
  } catch (error) {
    const message = error instanceof Error ? error.message : String(error);

    return json(
      {
        outcome: "error",
        message: `Unhandled error: ${message}`,
      },
      500,
    );
  }
});