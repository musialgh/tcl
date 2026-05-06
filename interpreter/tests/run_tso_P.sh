#/bin/bash

basename="test_final_round_tso_P_skating"
input="${basename}_in.json"
reference="${basename}_out.json"
output="_out.json"
html_report="_report.html"
html_places="_places.html"

dotnet build ../TclInterpreter.csproj                                || exit 1

tcl wrap_round_final_skating $1 --input-file ${input}     >${output} || exit 1

./json-to-table.py -t Report  -k report -o ${html_report} <${output} || exit 1
./json-to-table.py -t Placing -k places -o ${html_places} <${output} || exit 1
