export type ActivityAiSource = {
  id: string;
  title: string;
  signupUrl: string | null;
  city: string | null;
  date: string;
};

export type AskAssistantResult = {
  answer: string;
  sources: ActivityAiSource[];
  errorMessage?: string;
};

export async function askActivityAssistant(
  question: string,
): Promise<AskAssistantResult> {
  try {
    const response = await fetch("/api/ai/ask", {
      method: "POST",
      headers: {
        Accept: "application/json",
        "Content-Type": "application/json",
      },
      body: JSON.stringify({ question }),
    });

    if (!response.ok) {
      const detail = await response.text();
      return {
        answer: "",
        sources: [],
        errorMessage: parseErrorDetail(detail, response.status, response.statusText),
      };
    }

    const payload: unknown = await response.json();

    if (
      typeof payload !== "object" ||
      payload === null ||
      typeof (payload as { answer?: unknown }).answer !== "string"
    ) {
      return {
        answer: "",
        sources: [],
        errorMessage: "Unexpected response from the assistant API.",
      };
    }

    const record = payload as {
      answer: string;
      sources?: unknown;
    };

    const sources = Array.isArray(record.sources)
      ? record.sources
          .map(parseSource)
          .filter((source): source is ActivityAiSource => source !== null)
      : [];

    return {
      answer: record.answer,
      sources,
    };
  } catch (error) {
    return {
      answer: "",
      sources: [],
      errorMessage:
        error instanceof Error
          ? error.message
          : "Could not reach the assistant API.",
    };
  }
}

function parseErrorDetail(body: string, status: number, statusText: string) {
  if (!body) {
    return `API returnerade ${status} ${statusText}.`;
  }

  try {
    const parsed: unknown = JSON.parse(body);

    if (typeof parsed === "object" && parsed !== null) {
      const record = parsed as Record<string, unknown>;
      const message =
        (typeof record.message === "string" && record.message) ||
        (typeof record.detail === "string" && record.detail) ||
        (typeof record.title === "string" && record.title);

      if (message) {
        return message;
      }
    }
  } catch {
    // Plain-text error from backend.
  }

  return body.length > 280 ? `${body.slice(0, 280)}…` : body;
}

function parseSource(value: unknown): ActivityAiSource | null {
  if (typeof value !== "object" || value === null) {
    return null;
  }

  const record = value as Record<string, unknown>;
  const id = typeof record.id === "string" ? record.id : null;
  const title = typeof record.title === "string" ? record.title : null;

  if (!id || !title) {
    return null;
  }

  return {
    id,
    title,
    signupUrl: typeof record.signupUrl === "string" ? record.signupUrl : null,
    city: typeof record.city === "string" ? record.city : null,
    date: typeof record.date === "string" ? record.date : "",
  };
}
