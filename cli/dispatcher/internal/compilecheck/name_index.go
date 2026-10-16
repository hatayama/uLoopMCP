package compilecheck

import (
	"bytes"
	"errors"
	"fmt"
	"io/fs"
	"os"
	"path/filepath"
	"slices"
	"strings"
	"time"
	"unicode"
	"unicode/utf8"
)

// targetTypedNewToken stands for a target-typed new in a dependent's identifiers, the way the worker
// names a changed constructor.
const targetTypedNewToken = "new("

// ChangedReference names a referenced assembly whose changed surface made a unit compile, and the
// first of its changed names the unit writes; empty when the change could not be told by names.
type ChangedReference struct {
	Assembly string
	Name     string
}

// NameSkip names a unit left out although a reference it reads changed its surface, because the unit
// writes none of the names the change touches, and those references.
type NameSkip struct {
	Assembly          string
	ChangedReferences []string
}

// NameIndexReport says what reading the identifiers of the units with references in the plan took.
type NameIndexReport struct {
	Duration time.Duration
	// Units counts the units whose identifiers were read, WithGenerated those whose saved generator
	// output was read too, and Unreadable those that could not be read and so compile on any change.
	Units         int
	WithGenerated int
	Unreadable    int
}

// identifierSet is the identifiers one unit writes.
type identifierSet map[string]struct{}

// scanIdentifiers adds every identifier content holds to into, cut the way C# cuts one: a letter, a
// letter number or '_', followed by those, decimal digits, combining marks, connectors and formatting
// characters, the last left out of the name as the compiler leaves them out. Comments, strings and code a #if leaves out are read too, which can only make a
// unit compile more often. A "new" followed by '(' also adds the target-typed new token.
// Why \u escapes are not decoded: almost nobody writes one in an identifier, and the miss is named
// among the limits of the check.
func scanIdentifiers(content []byte, into identifierSet) {
	position := 0
	for position < len(content) {
		character, size := utf8.DecodeRune(content[position:])
		if !isIdentifierStart(character) {
			position += size

			continue
		}
		start := position
		position += size
		for position < len(content) {
			next, nextSize := utf8.DecodeRune(content[position:])
			if !isIdentifierPart(next) {
				break
			}
			position += nextSize
		}
		identifier := identifierText(content[start:position])
		into[identifier] = struct{}{}
		if identifier == "new" && opensParenthesis(content[position:]) {
			into[targetTypedNewToken] = struct{}{}
		}
	}
}

func isIdentifierStart(character rune) bool {
	return unicode.IsLetter(character) || unicode.Is(unicode.Nl, character) || character == '_'
}

func isIdentifierPart(character rune) bool {
	return isIdentifierStart(character) || unicode.In(character, unicode.Nd, unicode.Mn, unicode.Mc, unicode.Pc, unicode.Cf)
}

// identifierText is the name an identifier's text spells, without its formatting characters.
func identifierText(text []byte) string {
	if !bytes.ContainsFunc(text, isFormatting) {
		return string(text)
	}

	return string(bytes.Map(func(character rune) rune {
		if isFormatting(character) {
			return -1
		}

		return character
	}, text))
}

func isFormatting(character rune) bool {
	return unicode.Is(unicode.Cf, character)
}

// opensParenthesis reports whether rest starts with '(' after any white space.
// Why it walks the bytes in place: copying the rest of the file at every "new" makes a file full of
// them take time in the square of its length.
func opensParenthesis(rest []byte) bool {
	trimmed := bytes.TrimLeftFunc(rest, unicode.IsSpace)

	return len(trimmed) > 0 && trimmed[0] == '('
}

// readSourceIdentifiers adds the identifiers of every source of a unit to into.
// Why a unit without sources is an error rather than an empty set: an empty set would read as a unit
// that writes no changed name, and skip it on every change.
func readSourceIdentifiers(projectRoot string, unit CompileUnit, into identifierSet) error {
	if len(unit.Sources) == 0 {
		return fmt.Errorf("%s has no sources to read names from", unit.Assembly.AssemblyName)
	}
	for _, source := range unit.Sources {
		content, err := os.ReadFile(sourcePath(projectRoot, filepath.FromSlash(source)))
		if err != nil {
			return fmt.Errorf("failed to read the names of %s: %w", source, err)
		}
		scanIdentifiers(content, into)
	}

	return nil
}

// readGeneratedIdentifiers reads the identifiers of the generator output a unit's last full compile
// saved, and reports whether there was any.
// Why generator output is read: a generator can write a referenced name the unit's own sources never
// do, and that code compiles with the unit.
func readGeneratedIdentifiers(generatedDirectory string) (identifierSet, bool, error) {
	identifiers := identifierSet{}
	if _, err := os.Stat(generatedDirectory); errors.Is(err, fs.ErrNotExist) {
		return identifiers, false, nil
	}
	err := filepath.WalkDir(generatedDirectory, func(path string, entry fs.DirEntry, walkErr error) error {
		if walkErr != nil {
			return walkErr
		}
		if entry.IsDir() || !strings.EqualFold(filepath.Ext(path), ".cs") {
			return nil
		}
		content, err := os.ReadFile(path)
		if err != nil {
			return err
		}
		scanIdentifiers(content, identifiers)

		return nil
	})
	if err != nil {
		return nil, false, fmt.Errorf("failed to read the generated names in %s: %w", generatedDirectory, err)
	}

	return identifiers, true, nil
}

// nameIndex reads, in the background, the identifiers of every unit that references another unit of
// the plan, so a reference that changed only names a unit never writes need not make it compile.
// Why every such unit and not only the dependents: a unit selected for its own sources compiles
// anyway, but one that started on the assumption that a reference keeps its surface need not compile
// again when that reference changed only names it never writes.
type nameIndex struct {
	sets          []identifierSet
	errs          []error
	withGenerated []bool
	indexed       []bool
	done          chan struct{}
	duration      time.Duration
	started       bool
}

// start reads the identifiers of the units marked in wanted, at most once per run. generated holds
// the generator output directory of each unit. The generator output is read before start returns and
// the sources in the background.
// Why the generator output is read first: a unit's compile removes and rewrites that directory, and a
// read racing it would lose the names only the generator wrote. Sources are never removed, and few
// units have saved generator output, so only the sources are worth reading while compiles run.
func (index *nameIndex) start(projectRoot string, units []CompileUnit, generated []string, wanted []bool) {
	if index.started {
		return
	}
	index.started = true
	index.done = make(chan struct{})
	index.indexed = wanted
	startedAt := time.Now()
	type unitNames struct {
		set           identifierSet
		withGenerated bool
		err           error
	}
	generatedNames := computeInParallel(len(units), func(position int) unitNames {
		if !wanted[position] {
			return unitNames{}
		}
		set, withGenerated, err := readGeneratedIdentifiers(generated[position])

		return unitNames{set: set, withGenerated: withGenerated, err: err}
	})
	go func() {
		defer close(index.done)
		results := computeInParallel(len(units), func(position int) unitNames {
			names := generatedNames[position]
			if !wanted[position] || names.err != nil {
				return names
			}
			if err := readSourceIdentifiers(projectRoot, units[position], names.set); err != nil {
				return unitNames{err: err}
			}

			return names
		})
		index.sets = make([]identifierSet, len(units))
		index.errs = make([]error, len(units))
		index.withGenerated = make([]bool, len(units))
		for position, result := range results {
			index.sets[position] = result.set
			index.errs[position] = result.err
			index.withGenerated[position] = result.withGenerated
		}
		index.duration = time.Since(startedAt)
	}()
}

// lookup waits for the index and returns the identifiers of one unit, or an error when it has none.
func (index *nameIndex) lookup(position int) (identifierSet, error) {
	if !index.started {
		return nil, errors.New("the names of the plan were never read")
	}
	<-index.done
	if !index.indexed[position] {
		return nil, errors.New("the names of this unit were not read")
	}
	if index.errs[position] != nil {
		return nil, index.errs[position]
	}

	return index.sets[position], nil
}

// wait returns once the background read is over, so nothing outlives the run that started it.
func (index *nameIndex) wait() {
	if index.started {
		<-index.done
	}
}

// report says what the read took; zero when nothing was read.
func (index *nameIndex) report() NameIndexReport {
	if !index.started {
		return NameIndexReport{}
	}
	<-index.done
	report := NameIndexReport{Duration: index.duration}
	for position, indexed := range index.indexed {
		if !indexed {
			continue
		}
		report.Units++
		if index.errs[position] != nil {
			report.Unreadable++
		} else if index.withGenerated[position] {
			report.WithGenerated++
		}
	}

	return report
}

// startNameIndex starts reading the identifiers of the units that reference another unit of the plan,
// when there is any.
func (run *schedulerRun) startNameIndex() {
	wanted := make([]bool, len(run.plan.Units))
	any := false
	for position := range run.plan.Units {
		wanted[position] = len(run.state.references[position]) > 0
		any = any || wanted[position]
	}
	if !any {
		return
	}
	records := run.compiler.records(run.plan)
	generated := make([]string, len(run.plan.Units))
	for position, unit := range run.plan.Units {
		generated[position] = filepath.Join(run.compiler.ProjectRoot, records.GeneratedDirectory(unit.Assembly.AssemblyName))
	}
	run.names.start(run.compiler.ProjectRoot, run.plan.Units, generated, wanted)
}

// referenceEffect is what one reference's final state means for one unit that reads it: whether it
// counts as a changed input, and the first of its changed names the unit writes.
type referenceEffect struct {
	dependent int
	touches   bool
	// named marks a reference whose change was told by names, so one that does not touch was
	// changed rather than kept.
	named bool
	name  string
}

// changedReference is a reference that touched a unit, by its position in the plan, with the first
// changed name the unit writes; empty when no name told the change.
type changedReference struct {
	reference int
	name      string
}

// effectsOf decides, for every unit reading the unit at index, whether its verdict touches that unit:
// never when kept; always when the change cannot be told by names, when nothing compared it, or when
// the unit's names could not be read; and otherwise when the unit writes one of the changed names.
func (run *schedulerRun) effectsOf(index int, verdict SurfaceVerdict) []referenceEffect {
	dependents := run.state.dependents[index]
	effects := make([]referenceEffect, 0, len(dependents))
	for _, dependent := range dependents {
		switch {
		case verdict.Kept:
			effects = append(effects, referenceEffect{dependent: dependent})
		case !verdict.namedChange():
			effects = append(effects, referenceEffect{dependent: dependent, touches: true})
		default:
			effects = append(effects, run.namedEffect(dependent, verdict.ChangedNames))
		}
	}

	return effects
}

func (run *schedulerRun) namedEffect(dependent int, changedNames []string) referenceEffect {
	identifiers, err := run.names.lookup(dependent)
	if err != nil {
		return referenceEffect{dependent: dependent, touches: true}
	}
	for _, name := range changedNames {
		if _, found := identifiers[name]; found {
			return referenceEffect{dependent: dependent, touches: true, named: true, name: name}
		}
	}

	return referenceEffect{dependent: dependent, named: true}
}

// changedReferencesOf names the references that touched the unit at index, in the plan's order.
func (run *schedulerRun) changedReferencesOf(index int) []ChangedReference {
	touched := slices.Clone(run.state.changedBy[index])
	if len(touched) == 0 {
		return nil
	}
	slices.SortFunc(touched, func(a changedReference, b changedReference) int { return a.reference - b.reference })
	references := make([]ChangedReference, 0, len(touched))
	for _, reference := range touched {
		references = append(references, ChangedReference{
			Assembly: run.plan.Units[reference.reference].Assembly.AssemblyName, Name: reference.name,
		})
	}

	return references
}

// recordNameSkip notes a unit left out although a reference changed, because it writes none of the
// names that reference's change touches.
func (run *schedulerRun) recordNameSkip(index int) {
	unaffected := run.state.unaffectedBy[index]
	if len(unaffected) == 0 {
		return
	}
	run.nameSkips = append(run.nameSkips, NameSkip{
		Assembly: run.plan.Units[index].Assembly.AssemblyName, ChangedReferences: run.namesOf(slices.Sorted(slices.Values(unaffected))),
	})
}
