import { route } from '@fixtures';

describe('Change Folder Paths', () => {
	beforeEach(() => {
		cy.basePageSetup({
			plexAccountCount: 0,
			plexServerCount: 0,
			invalidDefaultFolderPaths: true,
		});

		cy.visit(route('/settings/paths'));
	});

	it('Should set the correct destination for each default folder when using the directory browser', () => {
		cy.getPageData().then(() => cy.correctDefaultFolderPaths());
	});

	it('keeps compact path tabs and grouped folder rows readable on mobile', () => {
		cy.viewport(320, 568);
		cy.getPageData();

		cy.get('.folder-path-tabs').then(($tabs) => {
			const tabs = $tabs[0]!;
			expect(tabs.scrollWidth, 'folder path tab overflow').to.be.lte(tabs.clientWidth + 1);
		});
		cy.get('.folder-path-tabs .q-tab').should('have.length', 3).each(($tab) => {
			expect($tab.attr('aria-label'), 'accessible tab name').not.to.equal('');
			expect($tab[0]!.getBoundingClientRect().right, 'tab stays inside viewport').to.be.lte(320);
		});
		cy.get('.folder-path-tab-label').should('not.be.visible');

		cy.getCy('folder-path-tab-movie-folder').click();
		cy.get('.folder-path-row')
			.should('be.visible')
			.and(($rows) => {
				for (const row of $rows) {
					const styles = getComputedStyle(row);
					expect(styles.borderTopStyle, 'row grouping border').to.equal('solid');
					expect(row.getBoundingClientRect().right, 'row stays inside viewport').to.be.lte(320);
				}
			});
		cy.getCy('default-movie-folder-row').then(($row) => {
			const rowRect = $row[0]!.getBoundingClientRect();
			const inputRect = $row.find('.folder-path-input')[0]!.getBoundingClientRect();
			expect(inputRect.left, 'folder input starts inside its row').to.be.gte(rowRect.left);
			expect(inputRect.right, 'folder input ends inside its row').to.be.lte(rowRect.right);
		});
		cy.getCy('default-movie-folder-edit-button').should(($button) => {
			const rect = $button[0]!.getBoundingClientRect();
			expect(rect.width, 'folder browser touch width').to.be.gte(44);
			expect(rect.height, 'folder browser touch height').to.be.gte(44);
		});
		cy.getCy('default-movie-folder-delete-button').should('not.exist');
	});
});
