import { NextResponse } from "next/server";

const DEFAULT_API_BASE_URL = "http://localhost:5289";

function getBackendApiBaseUrl() {
  const configured = process.env.BARNAKTIV_API_BASE_URL?.trim();
  return configured?.replace(/\/$/, "") || DEFAULT_API_BASE_URL;
}

export async function POST(request: Request) {
  let body: unknown;

  try {
    body = await request.json();
  } catch {
    return NextResponse.json({ message: "Ogiltig JSON i förfrågan." }, { status: 400 });
  }

  const apiBaseUrl = getBackendApiBaseUrl();

  try {
    const response = await fetch(`${apiBaseUrl}/api/ai/ask`, {
      method: "POST",
      headers: {
        Accept: "application/json",
        "Content-Type": "application/json",
      },
      body: JSON.stringify(body),
      cache: "no-store",
    });

    const text = await response.text();

    return new NextResponse(text, {
      status: response.status,
      headers: {
        "Content-Type": response.headers.get("Content-Type") ?? "application/json",
      },
    });
  } catch (error) {
    const detail =
      error instanceof Error ? error.message : "Kunde inte nå Barnaktiv API.";

    return NextResponse.json(
      {
        message: `${detail} Starta Barnaktiv.API (${apiBaseUrl}) och försök igen.`,
      },
      { status: 502 },
    );
  }
}
