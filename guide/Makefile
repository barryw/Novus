# Novus Programmer's Reference Guide - Build System

LATEX = pdflatex
MAIN = main
OUTPUT = novus-guide.pdf

# Build the PDF (run twice for TOC/references)
all: $(OUTPUT)

CHAPTERS = $(wildcard chapters/*.tex)

$(OUTPUT): $(MAIN).tex cover.tex $(CHAPTERS)
	$(LATEX) -jobname=novus-guide $(MAIN).tex
	$(LATEX) -jobname=novus-guide $(MAIN).tex
	@echo ""
	@echo "Built: $(OUTPUT)"

# Quick build (single pass, faster for drafts)
quick:
	$(LATEX) -jobname=novus-guide $(MAIN).tex

# Clean auxiliary files
clean:
	rm -f *.aux *.log *.toc *.out *.fls *.fdb_latexmk *.synctex.gz

# Clean everything including PDF
distclean: clean
	rm -f $(OUTPUT)

# Watch for changes and rebuild (requires fswatch)
watch:
	@echo "Watching for changes... (Ctrl+C to stop)"
	fswatch -o *.tex | xargs -n1 -I{} make quick

.PHONY: all quick clean distclean watch
