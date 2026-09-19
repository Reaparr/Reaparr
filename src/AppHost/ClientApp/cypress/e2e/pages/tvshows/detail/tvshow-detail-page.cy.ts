import { route } from '@fixtures';
import { PlexMediaType } from '@dto';

describe('TV-Show Detail Page', () => {
	beforeEach(() => {
		cy.basePageSetup({
			plexServerCount: 1,
			plexTvShowLibraryCount: 1,
			tvShowCount: 1,
			seasonCount: 5,
			episodeCount: 10,
		}).then(({ mediaData }) => {
			const testData = mediaData.find((x) => x.media.some((y) => y.type === PlexMediaType.TvShow));
			cy.wrap(testData).should('not.be.undefined');
			if (!testData) {
				return;
			}
			cy.visit(route(`/tvshows/${testData.libraryId}/details/${testData.media[0]!.id}`));
		});
	});

	it('Should show selected value when the root select box is clicked', () => {
		cy.getPageData().then(() => {
			cy.getCy('media-list-root-checkbox').click();
			cy.getCy('media-list-root-total-selected-count').should('contain.text', '50');
			cy.getCy('media-overview-bar-download-button').should('be.visible');
		});
	});

	it('shows episode controls without horizontal overflow on mobile', () => {
		cy.viewport(320, 568);
		cy.getPageData();
		cy.getCy('media-list-root-checkbox').should(($checkbox) => {
			expect($checkbox.attr('aria-label'), 'root selection accessible name').not.to.equal(undefined);
			expect($checkbox.attr('aria-label'), 'root selection accessible name').not.to.equal('');
			const rect = $checkbox[0]!.getBoundingClientRect();
			expect(rect.width, 'root selection touch width').to.be.gte(44);
			expect(rect.height, 'root selection touch height').to.be.gte(44);
		});
		cy.get('[data-cy^="media-list-child-checkbox-"]').first().should(($checkbox) => {
			expect($checkbox.attr('aria-label'), 'season selection accessible name').not.to.equal(undefined);
			expect($checkbox.attr('aria-label'), 'season selection accessible name').not.to.equal('');
			const rect = $checkbox[0]!.getBoundingClientRect();
			expect(rect.width, 'season selection touch width').to.be.gte(44);
			expect(rect.height, 'season selection touch height').to.be.gte(44);
		});
		cy.getCy('media-list-root-checkbox').closest('.q-list').find('.q-expansion-item > .q-expansion-item__container > .q-item').first().click();

		cy.getCy('media-q-table-mobile-card')
			.filter(':visible')
			.should('be.visible')
			.each(($card) => {
				const cardRect = $card[0]!.getBoundingClientRect();
				expect(cardRect.left, 'episode card left').to.be.gte(0);
				expect(cardRect.right, 'episode card right').to.be.lte(320);
			});
		cy.getCy('media-q-table-mobile-download')
			.filter(':visible')
			.should('be.visible')
			.each(($button) => {
				const buttonRect = $button[0]!.getBoundingClientRect();
				expect(buttonRect.width, 'download control width').to.be.gte(44);
				expect(buttonRect.height, 'download control height').to.be.gte(44);
				expect(buttonRect.right, 'download control right').to.be.lte(320);
			});
		cy.document().then((document) => {
			expect(document.documentElement.scrollWidth, 'page horizontal overflow').to.be.lte(
				document.documentElement.clientWidth + 1,
			);
		});
	});

	const detailViewports = [
		{ width: 320, height: 568 },
		{ width: 568, height: 320 },
		{ width: 768, height: 1024 },
		{ width: 1280, height: 800 },
	] as const;

	for (const viewport of detailViewports) {
		it(`keeps the detail header inside ${viewport.width}x${viewport.height}`, () => {
			cy.viewport(viewport.width, viewport.height);
			cy.getPageData();
			cy.get('.media-detail-header').should(($header) => {
				const rect = $header[0]!.getBoundingClientRect();
				expect(rect.left, 'detail header left').to.be.gte(0);
				expect(rect.right, 'detail header right').to.be.lte(viewport.width);
				expect($header[0]!.scrollWidth, 'detail header horizontal overflow').to.be.lte($header[0]!.clientWidth + 1);
			});
			cy.get('.media-info-container').should(($info) => {
				const rect = $info[0]!.getBoundingClientRect();
				expect(rect.left, 'media information left').to.be.gte(0);
				expect(rect.right, 'media information right').to.be.lte(viewport.width);
			});
			cy.document().then((document) => {
				expect(document.documentElement.scrollWidth, 'page horizontal overflow').to.be.lte(
					document.documentElement.clientWidth + 1,
				);
			});
		});
	}
});
