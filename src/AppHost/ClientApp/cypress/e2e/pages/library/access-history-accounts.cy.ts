import dayjs from 'dayjs';
import { PlexAccessState, type PlexLibraryAccessTimelineDTO, type PlexLibraryDTO, type PlexServerDTO } from '@dto';
import { PlexLibraryPaths } from '@api-urls';
import { generateResultDTO } from '@mock';
import { headers, route } from '@fixtures';

function relativeTimestamp(daysAgo: number, hour: number): string {
	const timestamp = new Date();
	timestamp.setDate(timestamp.getDate() - daysAgo);
	timestamp.setHours(hour, 0, 0, 0);
	return timestamp.toISOString();
}

function displayTimestamp(timestamp: string): string {
	return dayjs(timestamp).format('YYYY-MM-DD HH:mm');
}

interface HistoricalAccount {
	id: number;
	name: string;
	grantedAt: string;
	revokedAt: string;
}

interface HistoryScenario {
	accounts: [HistoricalAccount, HistoricalAccount];
	timeline: PlexLibraryAccessTimelineDTO;
}

function createScenario(server: PlexServerDTO, library: PlexLibraryDTO): HistoryScenario {
	const accounts: HistoryScenario['accounts'] = [
		{ id: 101, name: 'Account A', grantedAt: relativeTimestamp(5, 8), revokedAt: relativeTimestamp(3, 10) },
		{ id: 202, name: 'Account B', grantedAt: relativeTimestamp(4, 9), revokedAt: relativeTimestamp(2, 11) },
	];
	const [a, b] = accounts;
	const transitions = [
		{ account: a, createdAt: a.grantedAt, state: PlexAccessState.Granted },
		{ account: b, createdAt: b.grantedAt, state: PlexAccessState.Granted },
		{ account: a, createdAt: a.revokedAt, state: PlexAccessState.Revoked },
		{ account: b, createdAt: b.revokedAt, state: PlexAccessState.Revoked },
	];
	return {
		accounts,
		timeline: {
			currentState: [],
			events: transitions.map(({ account, createdAt, state }, index) => ({
				createdAt,
				id: index + 1,
				plexAccountId: account.id,
				plexAccountName: account.name,
				plexLibraryId: library.id,
				plexLibraryName: library.title,
				plexServerId: server.id,
				plexServerName: server.name,
				refreshRunId: `00000000-0000-0000-0000-${String(index + 1).padStart(12, '0')}`,
				state,
			})),
		},
	};
}

function taskBar(index: number) {
	return cy.getCy('library-access-timeline-gantt').find('[data-row-id]').eq(index).then(($row) => {
		const labelId = $row.attr('id');
		if (!labelId || !labelId.includes('child_')) throw new Error('Expected a JSGantt labelled task row');
		// JSGantt renders the label and its coloured bar in separate panes.
		const barId = labelId.replace('child_', 'taskbar_');
		return cy.getCy('library-access-timeline-gantt').find(`[id="${barId}"]`);
	});
}

function assertNotes(index: number, account: HistoricalAccount, server: PlexServerDTO, library: PlexLibraryDTO): void {
	taskBar(index).trigger('mouseover');
	cy.get('.JSGanttToolTip:visible')
		.should('contain.text', `Account: ${account.name}`)
		.and('contain.text', `Server: ${server.name}`)
		.and('contain.text', `Library: ${library.title}`)
		.and('contain.text', `Granted: ${displayTimestamp(account.grantedAt)}`)
		.and('contain.text', `Revoked: ${displayTimestamp(account.revokedAt)}`);
	taskBar(index).trigger('mouseout');
}

function assertSelectedRow(index: number, rowId: string): void {
	cy.getCy('library-access-timeline-gantt').find(`[data-row-id="${rowId}"]`).click();
	taskBar(index).should('have.class', 'gtaskblue');
	cy.getCy('library-access-timeline-gantt').find('.gtaskblue').should('have.length', 1);
}

function assertHistories(scenario: HistoryScenario, server: PlexServerDTO, library: PlexLibraryDTO): void {
	cy.getCy('library-access-timeline-gantt').find('[data-row-id]')
		.should('have.length', 2)
		.then(($tasks) => {
			const [firstRowId, secondRowId] = [...$tasks].map((task) => task.getAttribute('data-row-id'));
			if (!firstRowId || !secondRowId) throw new Error('Expected both account row IDs');
			expect(firstRowId).not.to.equal(secondRowId);
			assertNotes(0, scenario.accounts[0], server, library);
			assertNotes(1, scenario.accounts[1], server, library);
			assertSelectedRow(0, firstRowId);
			assertSelectedRow(1, secondRowId);
		});
}

describe('Library access history accounts', () => {
	it('loads retained account histories without current Plex accounts and keeps same-library intervals paired', () => {
		cy.basePageSetup({
			plexAccountCount: 0,
			plexServerCount: 1,
			plexMovieLibraryCount: 1,
			plexTvShowLibraryCount: 0,
			movieCount: 0,
		}).then(({ plexServers, plexLibraries }) => {
			const server = plexServers[0];
			const library = plexLibraries[0];
			if (!server || !library) throw new Error('Expected one mocked Plex server and library');
			const scenario = createScenario(server, library);
			cy.intercept('GET', PlexLibraryPaths.getPlexLibraryAccessTimelineEndpoint(), {
				statusCode: 200,
				body: generateResultDTO(scenario.timeline),
				...headers,
			}).as('accessTimeline');

			cy.visit(route('/library/access-history'));
			cy.getPageData();
			cy.wait('@accessTimeline').then(({ request, response }) => {
				expect(request.method).to.equal('GET');
				expect(request.query).to.deep.equal({});
				expect(response?.statusCode).to.equal(200);
			});
			assertHistories(scenario, server, library);
		});
	});
});

describe('Library access timeline local filters', () => {
	it('filters server, library and zoom without refetching, then refreshes on page reload', () => {
		cy.basePageSetup({
			seed: 113,
			plexAccountCount: 0,
			plexServerCount: 2,
			plexMovieLibraryCount: 2,
			plexTvShowLibraryCount: 0,
			movieCount: 0,
		}).then(({ plexServers, plexLibraries }) => {
			const serverA = plexServers[0]!;
			const serverB = plexServers[1]!;
			const [recentLibrary, olderLibrary] = plexLibraries.filter((library) => library.plexServerId === serverA.id);
			const otherLibrary = plexLibraries.find((library) => library.plexServerId === serverB.id)!;
			if (!recentLibrary || !olderLibrary) throw new Error('Expected two libraries on the first server');
			const histories = [
				{ accountId: 101, library: recentLibrary, grantedAt: relativeTimestamp(5, 10), revokedAt: relativeTimestamp(3, 16) },
				{ accountId: 102, library: olderLibrary, grantedAt: relativeTimestamp(20, 10), revokedAt: relativeTimestamp(10, 16) },
				{ accountId: 103, library: otherLibrary, grantedAt: relativeTimestamp(4, 10), revokedAt: relativeTimestamp(2, 16) },
			];
			const timeline: PlexLibraryAccessTimelineDTO = {
				currentState: [],
				events: histories.flatMap((history, index) => [PlexAccessState.Granted, PlexAccessState.Revoked].map((state, transition) => ({
					id: index * 2 + transition + 1,
					plexAccountId: history.accountId,
					plexAccountName: `Account ${history.accountId}`,
					plexServerId: history.library.plexServerId,
					plexServerName: plexServers.find((server) => server.id === history.library.plexServerId)!.name,
					plexLibraryId: history.library.id,
					plexLibraryName: history.library.title,
					createdAt: transition === 0 ? history.grantedAt : history.revokedAt,
					refreshRunId: `${history.accountId}-${transition}`,
					state,
				}))),
			};
			const rowIds = histories.map((history) => `account-${history.accountId}-server-${history.library.plexServerId}-library-${history.library.id}`);
			const assertRows = (expected: string[]) => {
				cy.get('[data-cy="library-access-timeline-gantt"] [data-row-id]').should(($rows) => {
					expect($rows.map((_, row) => row.getAttribute('data-row-id')).get().sort()).to.deep.equal([...expected].sort());
				});
			};
			cy.intercept('GET', PlexLibraryPaths.getPlexLibraryAccessTimelineEndpoint(), {
				statusCode: 200,
				body: generateResultDTO(timeline),
				...headers,
			}).as('localAccessTimeline');
			// Install before RxJS schedules intervals; leave UI animation timeouts running.
			cy.clock(Date.now(), ['Date', 'setInterval', 'clearInterval']).as('timelineClock');
			cy.visit(route('/library/access-history'));
			cy.getPageData();
			cy.wait('@localAccessTimeline').its('request.query').should('deep.equal', {});
			assertRows([rowIds[0]!, rowIds[2]!]);

			cy.get('[data-cy="library-access-timeline-server-filter"]').click();
			cy.contains('[role="option"]', serverA.name).click();
			cy.tick(250);
			assertRows([rowIds[0]!]);
			cy.get('[data-cy="library-access-timeline-zoom-filter"]').contains('button', 'All').click();
			cy.tick(250);
			assertRows([rowIds[0]!, rowIds[1]!]);
			cy.get('[data-cy="library-access-timeline-gantt"]').should('have.attr', 'data-zoom-preset', 'All');
			cy.get('[data-cy="library-access-timeline-library-filter"]').click();
			cy.contains('[role="option"]', olderLibrary.title).click();
			cy.tick(250);
			assertRows([rowIds[1]!]);
			cy.get('[data-cy="library-access-timeline-zoom-filter"]').contains('button', '7d').click();
			cy.tick(250);
			cy.get('[data-cy="library-access-timeline-chart"] [data-row-id]').should('not.exist');
			cy.get('[data-cy="library-access-timeline-server-filter"]').click();
			cy.contains('[role="option"]', serverB.name).click();
			cy.tick(250);
			assertRows([rowIds[2]!]);
			cy.get('[data-cy="library-access-timeline-gantt"]').should('have.attr', 'data-zoom-preset', '7d');
			cy.get('@localAccessTimeline.all').should('have.length', 1);

			cy.get('@timelineClock').invoke('restore');
			cy.reload();
			cy.getPageData();
			cy.wait('@localAccessTimeline').its('request.query').should('deep.equal', {});
			assertRows([rowIds[0]!, rowIds[2]!]);
			cy.get('@localAccessTimeline.all').should('have.length', 2);
		});
	});
});
