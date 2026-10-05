import type { IBasePageSetupResult } from '@fixtures';
import type { MockConfig } from '@mock';
// eslint-disable-next-line @typescript-eslint/consistent-type-imports
import { type PlexServerConnectionDTO, type PlexServerDTO, JobStatus, JobTypes, type PlexAccountDTO } from '@dto';

/* eslint-disable @typescript-eslint/no-namespace */
declare global {
	namespace Cypress {
		// 🤔 unsure why this Subject is unused, nor what to do with it...
		// eslint-disable-next-line @typescript-eslint/no-unused-vars
		interface Chainable<Subject> {
			/**
       * Custom command to set up the base page request interceptions for e2e tests
       * @example cy.basePageSetup({ plexAccountCount: 2, plexServerCount: 5 })
       */
			basePageSetup(config: Partial<MockConfig>): Chainable<IBasePageSetupResult>;

			getPageData(): Chainable<IBasePageSetupResult>;

			visitEmptyPage(): Chainable;

			getCy<E extends Node = HTMLElement>(
				selector: string,
				options?: Partial<Loggable & Timeoutable & Withinable & Shadow>
			): Chainable<JQuery<E>>;

			hubPublishJobStatusUpdate<T>(type: JobTypes, status: JobStatus, data: T): Chainable;

			hubPublishCheckPlexServerConnectionsJob(
				status: JobStatus,
				servers: PlexServerDTO[],
				connections: PlexServerConnectionDTO[]
			): Chainable;

			hubPublishInspectPlexServerJob(status: JobStatus, plexServerIds: number[]): Chainable;

			interceptAuthenticationStatus(loggedIn: boolean, pageLoadDelay?: number): Chainable;

			createPlexAccount(account: PlexAccountDTO | null): Chainable;

			correctDefaultFolderPaths(): Chainable;

			awaitSettingsUpdate(): Chainable;
		}
	}
}
