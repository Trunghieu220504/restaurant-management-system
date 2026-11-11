import { setupWorker } from "msw/browser";
import { identityHandlers } from "./handlers/identity";

export const worker = setupWorker(...identityHandlers);
