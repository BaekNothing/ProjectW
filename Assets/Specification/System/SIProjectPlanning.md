# SI Project Planning

## Document Control

- Version: 1.4
- Status: Approved for implementation
- Action: Create
- SSOT Change: Yes
- Source: `Docs/Design/SIProjectManagementConcept.md`
- Rationale: Replace the central effort ring and full-width card stack with a top summary-card row
  and an aligned responsive Task-card grid while preserving all planning interactions.

## Scope

This specification owns the modern SI stakeholder-request planning loop. It supersedes the
`60 / 30 / 10` weighted-slot planning presentation in `TaskSystem.md` for catalog-planned requests.
Legacy save compatibility may retain old fields, but new authored requests use this specification.

## Player Loop

1. Open an incomplete customer request delivered by mail.
2. Spend schedule time on a call or meeting, or proceed with explicit uncertainty.
3. Enter the planning board.
4. Toggle finite catalog Tasks into or out of project scope.
5. Select only the extension options needed by the interpreted request.
6. Review person-days, missing information, option conflicts, staffing fit, and forecast dates.
7. Assign one primary worker to every included Task.
8. Submit the locked plan for customer review.
9. Revise a rejected plan or execute an accepted plan through the normal Task queue.
10. Deliver after every included required Task completes.

## Catalog Task

Each request authors a finite array of catalog Tasks. A catalog Task contains:

- stable ID and short display name;
- base description and base person-days;
- required role and competency grades;
- required information;
- default scope state and hidden target scope state;
- approximately three extension options.

The initial playable request uses six catalog Tasks: `회원과 권한`, `상품과 주문`, `결제와 환불`,
`알림과 소통`, `조회와 보고`, and `관리자와 운영`. The broader ten-item catalog in the concept
document remains a candidate pool, not a requirement that every request display all ten items.

## Scope State

An excluded Task is greyed out and contributes no work, assignee, or schedule. Excluding it clears
its selected options and provisional worker. An included Task contributes its base work, enables
options, and must have a worker before submission.

## Extension Option

Each extension option contains a short name, explanation, additional person-days, required
information, risk notes, and optional conflict IDs. Selecting an option immediately adds its work.
Options are not pure upgrades: both omitting a needed option and adding an unneeded option reduce
request fit. Conflicting selected options produce a visible risk and do not silently resolve one
another.

## Planning Board

The planning board has no central circular effort control. It presents total person-days first in a
top summary-card row, followed by selected Task and option counts, forecast completion, customer
deadline, and schedule slack or overrun. The catalog Tasks appear in an aligned responsive card
grid: the normal landscape layout uses three equal-width columns and two rows for the initial six
Tasks, while narrower layouts wrap to fewer columns without changing card order. Cards in the same
row stretch to a common height so headings, controls, and following rows remain aligned. Every Task
card shows:

- ON/OFF state;
- base and extension person-days;
- explicit extension ON/OFF controls and detail actions;
- provisional worker and compact fit summary.

Each card has an explicit `ON/OFF` scope toggle. Excluded cards remain readable but state that their
work, worker, and schedule contribution are disabled. Included cards expose extension toggles and
worker selection. Missing staffing and option conflicts are written directly in the card as well as
in the board-level risk summary; presentation must not depend on color alone.

The existing detail-dialog rules remain unchanged: the Task detail action shows purpose, base work,
required information, and role; each option detail action shows added work, required information,
and new risks. The selected worker summary opens the existing worker-detail popup with its normal
dismissal rules. The board also lists blocking risks such as missing workers, insufficient scope,
deadline overrun, and authored option conflicts.

## Estimation and Schedule

Total person-days are the sum of included Task base work and selected extension work. Person-days
exist before staffing. Once staffed, the existing worker-efficiency calculation converts remaining
work into forecast duration. The request forecast starts after a pending customer-review date and
uses the latest completion among included Tasks.

The prototype retains one primary worker per Task. Multiple workers per Task and detailed manual
start-date editing remain explicitly undecided.

## Customer Review and Fit

Submission is blocked while clarification is pending, while an included Task is unstaffed, or when
fewer than the request's authored minimum Tasks are included. Submission locks the plan until the
authored review date.

Catalog fit compares every Task scope decision and every option decision with the request's hidden
target. This represents whether the plan understood the customer's actual need without rewarding
unnecessary over-specification. The existing acceptance threshold, review delay, rejection limit,
soft-deadline multiplier, and immutable delivery score continue to apply.

## Save Compatibility

Runtime Tasks persist scope and selected option IDs in the campaign snapshot. On restore, catalog
definitions recalculate required work from base work plus valid selected options. Missing arrays
normalize to empty arrays. Legacy weighted-slot requests continue through their existing path.

## AOT and Delivery Boundary

Catalog rules, data, and runtime controller logic belong to `ProjectW.HotUpdate`. The card
presentation reuses the generic card, button, assignment-summary, and modal styles already embedded
in base APK v18, so this presentation change is patch-only. The previously embedded annular sprite
and ring styles may remain inert in the base APK; active HotUpdate code must not depend on them.

## Deferred Decisions

The following remain outside version 1.0: final ability names, a universal catalog size, multiple
workers on one Task, full manual scheduling, systemic bug generation, resignation probability,
hiring candidate generation, financial reward balance, campaign end conditions, and final score.
