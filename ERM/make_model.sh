#!/usr/bin/bash

./erm_converter.py \
  --output Implementation/model.cs \
  --namespace TasModel \
  --entity_base TasEntity \
  --virtual-base nullable-fks \
  --virtual-base organization=base-and-kind \
  Model/*.excalidraw
