# Barnaktiv

BarnAktiv is a platform that collects children's activities in Gothenburg and displays them in one place.

The goal is to help parents discover activities such as:

- Football
- Gymnastics
- Swimming
- Horse riding
- Art classes

## Tech Stack

Frontend
- Next.js
- TypeScript

Backend
- .NET Web API

Data Collection
- Web scraping

## Architecture

The backend follows **Clean Architecture** principles:

- **Domain**: Core business entities and rules
- **Application**: Use cases, interfaces, DTOs, and business logic
- **Infrastructure**: Database access, repositories, and external concerns
- **API**: HTTP endpoints and composition root

This separation keeps business logic independent from frameworks and makes the solution easier to test and maintain.

## Vision

Instead of searching many websites, parents can find all activities in one place.

## Status

Clean Architecture foundation is in place, and core activity endpoints are under active development.
