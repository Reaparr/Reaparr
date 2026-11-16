import type { ITelemetry } from './ITelemetry';

/** Obtain once during setup; features only depend on the business telemetry contract. */
export function useTelemetry(): ITelemetry {
	return useNuxtApp().$telemetry;
}

declare module '#app' {
	interface NuxtApp {
		$telemetry: ITelemetry;
	}
}
