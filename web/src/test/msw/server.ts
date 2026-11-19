import { setupServer } from "msw/node";
import { identityHandlers } from "./handlers/identity";

export const server = setupServer(...identityHandlers);
