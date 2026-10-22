import { orderBy } from 'lodash-es';
import type { BaseResultDTO, PlexServerConnectionDTO } from '@dto';
import { SortDirection } from '@dto';
import { getSortOrder } from './useFlexQueryDsl';

export function sortPlexServerConnections(connections: PlexServerConnectionDTO[]) {
	const keysOrder: (keyof PlexServerConnectionDTO)[] = ['local', 'isPlexTvConnection'];
	return orderBy(connections, keysOrder, getSortOrder(SortDirection.Asc));
}

export function waitForElement(parentElement: HTMLElement | null, selector: string, intervalMs: number = 100): Promise<HTMLElement | null> {
	return new Promise((resolve) => {
		const timeout = setTimeout(() => {
			clearInterval(checkExist);
			resolve(null); // Resolve with null if timeout
		}, 5000);

		const checkExist = setInterval(() => {
			const element = parentElement?.querySelector(selector) as HTMLElement | null;
			if (element) {
				clearInterval(checkExist);
				clearTimeout(timeout);
				resolve(element);
			}
		}, intervalMs);
	});
}

export function discordInviteLink() {
	return 'https://discord.com/invite/k2KvDJbnNm';
}

export function formatErrorResponse(res: unknown): string {
	if (!res) return '❌ An unknown error occurred.';
	if (typeof res === 'string') return res;
	if (res instanceof Error) return res.message;
	if (typeof res !== 'object' || !('errors' in res)) return '❌ An unknown error occurred.';

	const result = res as BaseResultDTO;
	const header = `❌ Error ${result.statusCode}`;
	const messages = result.errors.map((err) => {
		const reasons
			= err.reasons && err.reasons.length
				? `\n   ↳ Reasons: ${err.reasons.map((r) => r?.message ?? String(r)).join(', ')}`
				: '';
		return `• ${err.message}${reasons}`;
	});
	return [header, ...messages].join('\n');
}
