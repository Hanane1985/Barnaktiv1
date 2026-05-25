"use client";

import Link from "next/link";
import { FormEvent, useState } from "react";

import { SiteFooter, SiteHeader } from "@/components/site-chrome";
import {
  askActivityAssistant,
  type ActivityAiSource,
} from "@/lib/ai-assistant";

type ChatMessage = {
  id: string;
  role: "user" | "assistant";
  content: string;
  sources?: ActivityAiSource[];
  errorMessage?: string;
};

const starterPrompts = [
  "Vad kan en 8-åring göra i Göteborg nästa helg?",
  "Finns det gratis fotboll för barn runt 10 år?",
  "Simning eller dans för 6–7 år i Göteborg",
];

export function ActivityAssistant() {
  const [messages, setMessages] = useState<ChatMessage[]>([]);
  const [input, setInput] = useState("");
  const [isLoading, setIsLoading] = useState(false);

  async function submitQuestion(question: string) {
    const trimmed = question.trim();
    if (!trimmed || isLoading) {
      return;
    }

    const userMessage: ChatMessage = {
      id: crypto.randomUUID(),
      role: "user",
      content: trimmed,
    };

    setMessages((current) => [...current, userMessage]);
    setInput("");
    setIsLoading(true);

    const result = await askActivityAssistant(trimmed);

    const assistantMessage: ChatMessage = {
      id: crypto.randomUUID(),
      role: "assistant",
      content: result.errorMessage
        ? "Assistenten kunde inte svara just nu."
        : result.answer || "Jag hittade inget att svara på.",
      sources: result.sources,
      errorMessage: result.errorMessage,
    };

    setMessages((current) => [...current, assistantMessage]);
    setIsLoading(false);
  }

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    void submitQuestion(input);
  }

  return (
    <div className="min-h-screen bg-[color:var(--background)] text-[color:var(--foreground)]">
      <SiteHeader />

      <main className="mx-auto max-w-3xl px-4 py-8 sm:px-6 lg:px-8">
        <div className="mb-8">
          <p className="text-sm font-medium text-[color:var(--accent)]">AI-assistent</p>
          <h1 className="font-display mt-2 text-3xl font-semibold tracking-tight sm:text-4xl">
            Fråga om barnaktiviteter
          </h1>
          <p className="mt-3 text-sm leading-relaxed text-[color:var(--muted)] sm:text-base">
            Beskriv vad ni söker i vanlig svenska. Assistenten söker i Barnaktivs databas och
            svarar med förslag du kan klicka vidare på.
          </p>
          <Link
            href="/"
            className="mt-4 inline-block text-sm font-medium text-[color:var(--accent)] hover:underline"
          >
            ← Tillbaka till alla aktiviteter
          </Link>
        </div>

        {messages.length === 0 ? (
          <div className="mb-6 flex flex-wrap gap-2">
            {starterPrompts.map((prompt) => (
              <button
                key={prompt}
                type="button"
                disabled={isLoading}
                onClick={() => void submitQuestion(prompt)}
                className="rounded-full border border-[color:var(--border)] bg-[color:var(--surface)] px-4 py-2 text-left text-sm text-[color:var(--foreground)] transition hover:border-[color:var(--accent)] disabled:opacity-60"
              >
                {prompt}
              </button>
            ))}
          </div>
        ) : null}

        <div className="space-y-4">
          {messages.map((message) => (
            <article
              key={message.id}
              className={`rounded-2xl border px-4 py-3 sm:px-5 sm:py-4 ${
                message.role === "user"
                  ? "ml-8 border-[color:var(--accent)]/30 bg-[color:var(--accent-soft)]"
                  : "mr-8 border-[color:var(--border)] bg-[color:var(--surface)]"
              }`}
            >
              <p className="text-xs font-semibold uppercase tracking-wide text-[color:var(--muted)]">
                {message.role === "user" ? "Du" : "Barnaktiv"}
              </p>
              <p className="mt-2 whitespace-pre-wrap text-sm leading-relaxed sm:text-base">
                {message.content}
              </p>

              {message.errorMessage ? (
                <p className="mt-2 text-sm text-red-600 dark:text-red-400">
                  {message.errorMessage}
                </p>
              ) : null}

              {message.sources && message.sources.length > 0 ? (
                <ul className="mt-4 space-y-2 border-t border-[color:var(--border)] pt-3">
                  {message.sources.map((source) => (
                    <li key={source.id}>
                      {source.signupUrl ? (
                        <a
                          href={source.signupUrl}
                          target="_blank"
                          rel="noreferrer"
                          className="text-sm font-medium text-[color:var(--accent)] hover:underline"
                        >
                          {source.title}
                        </a>
                      ) : (
                        <span className="text-sm font-medium">{source.title}</span>
                      )}
                      <p className="text-xs text-[color:var(--muted)]">
                        {[source.city, formatDateLabel(source.date)]
                          .filter(Boolean)
                          .join(" · ")}
                      </p>
                    </li>
                  ))}
                </ul>
              ) : null}
            </article>
          ))}
        </div>

        <form
          onSubmit={handleSubmit}
          className="sticky bottom-4 mt-8 rounded-2xl border border-[color:var(--border)] bg-[color:var(--surface-strong)]/95 p-3 shadow-lg backdrop-blur-md sm:p-4"
        >
          <label htmlFor="assistant-question" className="sr-only">
            Din fråga
          </label>
          <textarea
            id="assistant-question"
            rows={3}
            value={input}
            onChange={(event) => setInput(event.target.value)}
            placeholder="Till exempel: Vi bor i Göteborg och söker något kul utomhus för en 9-åring…"
            maxLength={500}
            disabled={isLoading}
            className="w-full resize-none rounded-xl border border-[color:var(--border)] bg-[color:var(--background)] px-3 py-2 text-sm text-[color:var(--foreground)] outline-none ring-[color:var(--accent)] focus:ring-2 disabled:opacity-60"
          />
          <div className="mt-3 flex items-center justify-between gap-3">
            <p className="text-xs text-[color:var(--muted)]">{input.length}/500</p>
            <button
              type="submit"
              disabled={isLoading || input.trim().length === 0}
              className="rounded-full bg-[color:var(--accent)] px-5 py-2 text-sm font-semibold text-white transition hover:opacity-90 disabled:cursor-not-allowed disabled:opacity-50"
            >
              {isLoading ? "Tänker…" : "Fråga"}
            </button>
          </div>
        </form>
      </main>

      <SiteFooter />
    </div>
  );
}

function formatDateLabel(value: string) {
  if (!value) {
    return null;
  }

  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return null;
  }

  return date.toLocaleDateString("sv-SE", {
    day: "numeric",
    month: "short",
    year: "numeric",
  });
}
